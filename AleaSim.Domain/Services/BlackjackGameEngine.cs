using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class BlackjackGameEngine : BaseGameEngine {
    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    public BlackjackGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {
    }

    public class BlackjackState {
        public List<string> PlayerHand { get; set; } = new();
        public List<string> DealerHand { get; set; } = new();
        public decimal BetAmount { get; set; }
        public bool IsRoundOver { get; set; }
        public int Sequence { get; set; }
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async repo => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");
            var lastBet = repo.GetLastBet(sessionId);
            if (lastBet == null) throw new Exception("No bet found");

            var state = new BlackjackState { BetAmount = lastBet.Amount, Sequence = 0 };
            int seq = 0;
            state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
            state.DealerHand.Add(DrawCard(session.Seed, ref seq));
            state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
            state.DealerHand.Add(DrawCard(session.Seed, ref seq));
            state.Sequence = seq;

            if (CalculateHandValue(state.PlayerHand) == 21) state.IsRoundOver = true;

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                TotalBetAmount = lastBet.Amount,
                ExecutedAt = DateTime.UtcNow,
                RandomResult = JsonSerializer.Serialize(state)
            };

            if (state.IsRoundOver) await FinishRoundAsync(session, round, state, repo);

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Blackjack", State = state });
            return round;
        });
    }

    public override async Task ProcessAction(Guid sessionId, string action, string actionData) {
        await ExecuteScopedAsync(async repo => {
            var session = repo.GetSession(sessionId);
            var round = repo.GetLastRound(sessionId);
            if (round == null) return;
            var state = JsonSerializer.Deserialize<BlackjackState>(round.RandomResult);
            if (state == null || state.IsRoundOver) return;

            if (action.ToLower() == "hit") {
                int seq = state.Sequence;
                state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
                state.Sequence = seq;
                if (CalculateHandValue(state.PlayerHand) >= 21) await FinishRoundAsync(session, round, state, repo);
            } else if (action.ToLower() == "stand") {
                await FinishRoundAsync(session, round, state, repo);
            }

            round.RandomResult = JsonSerializer.Serialize(state);
            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Action = action, State = state });
        });
    }

    private async Task FinishRoundAsync(GameSession session, GameRound round, BlackjackState state, IGameRepository repo) {
        state.IsRoundOver = true;
        int pVal = CalculateHandValue(state.PlayerHand);
        int dVal = CalculateHandValue(state.DealerHand);
        decimal win = (pVal > 21) ? 0 : (pVal > dVal || dVal > 21) ? state.BetAmount * 2 : (pVal == dVal) ? state.BetAmount : 0;

        if (win > 0) VaultService.ProcessWin(session.UserId, win, repo);
        BrainService.UpdateProfile(session.UserId, state.BetAmount, win);
        round.TotalWinAmount = win;
        await Task.CompletedTask;
    }

    private string DrawCard(int seed, ref int seq) {
        seq++;
        int idx = RngService.GetNextInt(seed, seq, 0, 52);
        return "A2345678910JQK"[idx % 13].ToString() + "HDCS"[idx / 13];
    }

    private int CalculateHandValue(List<string> hand) => 20; // Simplified for rebuild
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome());
    public override Task<object?> GetCurrentState(Guid sessionId) => Task.FromResult<object?>(null);
}