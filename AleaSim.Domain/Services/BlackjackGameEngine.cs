using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class BlackjackGameEngine : BaseGameEngine {
    
    public BlackjackGameEngine(IRngService rngService, IVaultService vaultService, IBrainService brainService, IPromotionService promotionService, IJackpotService jackpotService, IRealTimeService realTimeService, IServiceScopeFactory scopeFactory) 
        : base(rngService, vaultService, brainService, promotionService, jackpotService, realTimeService, scopeFactory) {
    }

    public class BlackjackState {
        public List<string> PlayerHand { get; set; } = new();
        public List<string> DealerHand { get; set; } = new();
        public decimal BetAmount { get; set; }
        public bool IsRoundOver { get; set; }
        public int Sequence { get; set; }
    }

    public override async Task PlaceBet(Guid sessionId, decimal amount, string betData) {
        await base.PlaceBet(sessionId, amount, betData);
        // We don't create state here yet, ResolveRound will initialize the deal.
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async repo => {
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
                    await FinishRoundAsync(session, round, state, repo);
                }

                transaction.Commit();

                // Notify UI about the deal
                await RealTimeService.NotifyGameUpdate(session.UserId, new {
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

    public override async Task ProcessAction(Guid sessionId, string action, string actionData) {
        await ExecuteScopedAsync(async repo => {
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
                         await FinishRoundAsync(session, round, state, repo);
                    } else {
                        round.RandomResult = JsonSerializer.Serialize(state);
                        repo.SaveRound(round);
                    }
                }
                else if (action.ToLower() == "stand") {
                    await FinishRoundAsync(session, round, state, repo);
                }
                
                transaction.Commit();

                // Notify UI about the action result
                await RealTimeService.NotifyGameUpdate(session.UserId, new {
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

    public override async Task<Outcome> GetOutcome(Guid roundId) {
        return await Task.Run(() => ExecuteScoped(repo => repo.GetOutcome(roundId) 
               ?? new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId }));
    }

    public override async Task<object?> GetCurrentState(Guid sessionId) {
        return await Task.Run(() => {
            var round = ExecuteScoped(repo => repo.GetLastRound(sessionId));
            if (round == null) return null;
            return JsonSerializer.Deserialize<BlackjackState>(round.RandomResult);
        });
    }

    private async Task FinishRoundAsync(GameSession session, GameRound round, BlackjackState state, IGameRepository repo) {
        state.IsRoundOver = true;
        
        int playerValue = CalculateHandValue(state.PlayerHand);
        
        // Capture initial dealer state before they draw
        var initialDealerHand = new List<string>(state.DealerHand);
        int initialSequence = state.Sequence;

        decimal winAmount = 0;
        bool rtpAccepted = false;
        int attempts = 0;
        const int MaxAttempts = 10;

        // Loop to find an outcome that satisfies Vault Solvency (by altering Dealer's draws)
        do {
            attempts++;
            
            // Restore state for retry
            state.DealerHand = new List<string>(initialDealerHand);
            int seq = initialSequence;

            // Vary the sequence slightly per attempt
            int currentSeq = seq + (attempts * 100); 

            if (playerValue <= 21) {
                while (CalculateHandValue(state.DealerHand) < 17) {
                    state.DealerHand.Add(DrawCard(session.Seed, ref currentSeq));
                }
            }
            // Update state sequence
            state.Sequence = currentSeq;

            int dealerValue = CalculateHandValue(state.DealerHand);
            winAmount = 0;

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
                 // VAULT CHECK
                 if (VaultService.CanAffordWin(session.UserId, session.GameId, winAmount, repo)) {
                     rtpAccepted = true;
                 }
            } else {
                rtpAccepted = true; // Loss or Push is always accepted
            }

        } while (!rtpAccepted && attempts < MaxAttempts);

        // Fallback: Force Dealer to Win if Vault rejected everything
        if (!rtpAccepted && playerValue <= 21) {
            winAmount = 0;
            // Force dealer hand to be 21 to beat player
            state.DealerHand = new List<string> { "10H", "AS" }; // Blackjack!
        }

        if (winAmount > 0) {
             VaultService.ProcessWin(session.UserId, winAmount, repo);
        }
        
        // Update Brain
        BrainService.UpdateProfile(session.UserId, state.BetAmount, winAmount);
        
        // Track Tournament Win
        PromotionService.ProcessWinActivity(session.UserId, winAmount, repo);

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
        await Task.CompletedTask;
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
