using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AleaSim.Domain.Services;

public class BlackjackGameEngine : BaseGameEngine {
    private readonly ConcurrentDictionary<Guid, BlackjackState> _states = new();
    private readonly ConcurrentDictionary<Guid, int> _roundCounters = new();

    public BlackjackGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService) : base(rngService, rtpEngine, jackpotService) {
    }

    public class BlackjackState {
        public List<string> PlayerHand { get; set; } = new();
        public List<string> DealerHand { get; set; } = new();
        public decimal BetAmount { get; set; }
        public bool IsRoundOver { get; set; }
        public int Sequence { get; set; }
    }

    public override void PlaceBet(Guid sessionId, decimal amount, string betData) {
        base.PlaceBet(sessionId, amount, betData);
        _states[sessionId] = new BlackjackState { BetAmount = amount, Sequence = 0 };
    }

    public override GameRound ResolveRound(Guid sessionId) {
        if (!_states.TryGetValue(sessionId, out var state)) throw new InvalidOperationException("No bet placed.");

        if (state.PlayerHand.Count == 0) {
            // Initial Deal
            state.PlayerHand.Add(DrawCard(sessionId));
            state.DealerHand.Add(DrawCard(sessionId));
            state.PlayerHand.Add(DrawCard(sessionId));
            state.DealerHand.Add(DrawCard(sessionId));
            
            if (CalculateHandValue(state.PlayerHand) == 21) state.IsRoundOver = true;
        }

        return CreateRoundResult(sessionId, state);
    }

    public override void ProcessAction(Guid sessionId, string action, string actionData) {
        if (!_states.TryGetValue(sessionId, out var state) || state.IsRoundOver) return;

        if (action.ToLower() == "hit") {
            state.PlayerHand.Add(DrawCard(sessionId));
            
            if (CalculateHandValue(state.PlayerHand) >= 21) FinishRound(sessionId, state);
        }
        else if (action.ToLower() == "stand") {
            FinishRound(sessionId, state);
        }
    }

    public override Outcome GetOutcome(Guid roundId) {
        return new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId };
    }

    private void FinishRound(Guid sessionId, BlackjackState state) {
        state.IsRoundOver = true;
        
        int playerValue = CalculateHandValue(state.PlayerHand);
        if (playerValue <= 21){
            // Dealer's turn
            while (CalculateHandValue(state.DealerHand) < 17) {
                state.DealerHand.Add(DrawCard(sessionId));
            }
        }

        int dealerValue = CalculateHandValue(state.DealerHand);
        decimal winAmount = 0;

        if (playerValue > 21) winAmount = 0; // Bust
        else if (dealerValue > 21 || playerValue > dealerValue) winAmount = state.BetAmount * 2;
        else if (playerValue == dealerValue) winAmount = state.BetAmount; // Push

        if (winAmount > 0) {
            UpdateBalance(ActiveSessions[sessionId].UserId, winAmount);
            RtpEngine.RecordWin(ActiveSessions[sessionId].GameId, ActiveSessions[sessionId].UserId, winAmount);
        }
    }

    private string DrawCard(Guid sessionId) {
        var state = _states[sessionId];
        state.Sequence++;
        int cardIndex = RngService.GetNextInt(ActiveSessions[sessionId].Seed, state.Sequence, 0, 52);
        string[] suits = { "H", "D", "C", "S" };
        string[] ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
        return ranks[cardIndex % 13] + suits[cardIndex / 13];
    }

    private int CalculateHandValue(List<string> hand) {
        int value = 0;
        int aces = 0;
        foreach (var card in hand) {
            string rank = card.Substring(0, card.Length - 1);
            if (rank == "A") aces++;
            else if (new[] { "J", "Q", "K" }.Contains(rank)) value += 10;
            else value += int.Parse(rank);
        }
        for (int i = 0; i < aces; i++) {
            if (value + 11 <= 21) value += 11;
            else value += 1;
        }
        
        return value;
    }

    private GameRound CreateRoundResult(Guid sessionId, BlackjackState state) {
        int roundNumber = _roundCounters.AddOrUpdate(sessionId, 1, (k, v) => v + 1);
        return new GameRound {
            Id = Guid.NewGuid(),
            GameSessionId = sessionId,
            RoundNumber = roundNumber,
            RandomResult = JsonSerializer.Serialize(state),
            TotalBetAmount = state.BetAmount,
            TotalWinAmount = state.IsRoundOver ? 0 : 0, // Placeholder
            ExecutedAt = DateTime.UtcNow
        };
    }
}
