using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class BlackjackGameEngine : BaseGameEngine {
    
    public BlackjackGameEngine(IRngService rngService, IRtpEngine rtpEngine, IJackpotService jackpotService, IRealTimeService realTimeService, IServiceScopeFactory scopeFactory) 
        : base(rngService, rtpEngine, jackpotService, realTimeService, scopeFactory) {
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
        // We don't create state here yet, ResolveRound will initialize the deal.
    }

    public override GameRound ResolveRound(Guid sessionId) {
        return ExecuteScoped(repo => {
            using var transaction = repo.BeginTransaction();
            try {
                var session = repo.GetSession(sessionId);
                if (session == null) throw new InvalidOperationException("Session not found.");
                
                var lastBet = repo.GetLastBet(sessionId);
                if (lastBet == null) throw new InvalidOperationException("No bet found.");
                
                int roundNumber = repo.GetRoundCount(sessionId) + 1;

                var state = new BlackjackState { BetAmount = lastBet.Amount, Sequence = 0 };
                
                // Initial Deal
                int seq = state.Sequence;
                state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
                state.DealerHand.Add(DrawCard(session.Seed, ref seq));
                state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
                state.DealerHand.Add(DrawCard(session.Seed, ref seq));
                state.Sequence = seq;
                
                if (CalculateHandValue(state.PlayerHand) == 21) {
                     state.IsRoundOver = true;
                }

                var round = new GameRound {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    RoundNumber = roundNumber,
                    InputData = JsonSerializer.Serialize(state),
                    RandomResult = JsonSerializer.Serialize(state),
                    TotalBetAmount = lastBet.Amount,
                    TotalWinAmount = 0, // Pending
                    ExecutedAt = DateTime.UtcNow
                };

                repo.SaveRound(round);

                // Link bet to round
                lastBet.GameRoundId = round.Id;
                repo.UpdateBet(lastBet);

                if (state.IsRoundOver) {
                    FinishRound(session, round, state, repo);
                }

                transaction.Commit();

                // Notify UI about the deal
                RealTimeService.NotifyGameUpdate(session.UserId, new {
                    SessionId = sessionId,
                    Game = "Blackjack",
                    State = state
                });

                return round;
            }
            catch {
                transaction.Rollback();
                throw;
            }
        });
    }

    public override void ProcessAction(Guid sessionId, string action, string actionData) {
        ExecuteScoped(repo => {
            using var transaction = repo.BeginTransaction();
            try {
                var session = repo.GetSession(sessionId);
                if (session == null) return;

                var round = repo.GetLastRound(sessionId);
                if (round == null) return;
                
                var outcome = repo.GetOutcome(round.Id);
                if (outcome != null) return; // Round already over

                var state = JsonSerializer.Deserialize<BlackjackState>(round.RandomResult);
                if (state == null || state.IsRoundOver) return;

                if (action.ToLower() == "hit") {
                    int seq = state.Sequence;
                    state.PlayerHand.Add(DrawCard(session.Seed, ref seq));
                    state.Sequence = seq;
                    
                    if (CalculateHandValue(state.PlayerHand) >= 21) {
                         FinishRound(session, round, state, repo);
                    } else {
                        round.RandomResult = JsonSerializer.Serialize(state);
                        repo.SaveRound(round);
                    }
                }
                else if (action.ToLower() == "stand") {
                    FinishRound(session, round, state, repo);
                }
                
                transaction.Commit();

                // Notify UI about the action result
                RealTimeService.NotifyGameUpdate(session.UserId, new {
                    SessionId = sessionId,
                    Game = "Blackjack",
                    Action = action,
                    State = state
                });
            }
            catch {
                transaction.Rollback();
                throw;
            }
        });
    }

    public override Outcome GetOutcome(Guid roundId) {
        return ExecuteScoped(repo => repo.GetOutcome(roundId) 
               ?? new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId });
    }

    public override object? GetCurrentState(Guid sessionId) {
        var round = ExecuteScoped(repo => repo.GetLastRound(sessionId));
        if (round == null) return null;
        return JsonSerializer.Deserialize<BlackjackState>(round.RandomResult);
    }

    private void FinishRound(GameSession session, GameRound round, BlackjackState state, IGameRepository repo) {
        state.IsRoundOver = true;
        
        int playerValue = CalculateHandValue(state.PlayerHand);
        
        if (playerValue <= 21) {
            int seq = state.Sequence;
            while (CalculateHandValue(state.DealerHand) < 17) {
                state.DealerHand.Add(DrawCard(session.Seed, ref seq));
            }
            state.Sequence = seq;
        }

        int dealerValue = CalculateHandValue(state.DealerHand);
        decimal winAmount = 0;

        if (playerValue > 21) {
            winAmount = 0; // Bust
        }
        else if (dealerValue > 21) {
            winAmount = state.BetAmount * 2; // Dealer Bust
        }
        else if (playerValue > dealerValue) {
            bool isBlackjack = playerValue == 21 && state.PlayerHand.Count == 2;
            if (isBlackjack) {
                winAmount = state.BetAmount * 2.5m; 
            } else {
                winAmount = state.BetAmount * 2; 
            }
        }
        else if (playerValue == dealerValue) {
            winAmount = state.BetAmount; // Push
        }

        if (winAmount > 0) {
             if (!RtpEngine.ProcessWin(session.GameId, session.UserId, winAmount, state.BetAmount, repo)) {
                 winAmount = 0; // RTP Violation enforced
             } else {
                 repo.UpdateUserBalance(session.UserId, winAmount);
             }
        }

        round.RandomResult = JsonSerializer.Serialize(state);
        round.TotalWinAmount = winAmount;

        var outcome = new Outcome {
            Id = Guid.NewGuid(),
            GameRoundId = round.Id,
            ResultJson = JsonSerializer.Serialize(state),
            WinAmount = winAmount
        };
        repo.SaveOutcome(outcome);
        repo.SaveRound(round);
    }

    private string DrawCard(int seed, ref int sequence) {
        sequence++;
        // We use ref sequence to ensure state progresses correctly
        int cardIndex = RngService.GetNextInt(seed, sequence, 0, 52);
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
}