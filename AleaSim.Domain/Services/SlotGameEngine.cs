using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class SlotGameEngine : BaseGameEngine {
    private static readonly int[][] _reelStrips = new[] {
        new[] { 1, 2, 3, 4, 5, 1, 2, 3 }, // Reel 1
        new[] { 1, 2, 3, 4, 5, 1, 2, 3 }, // Reel 2
        new[] { 1, 2, 3, 4, 5, 1, 2, 3 }  // Reel 3
    };

    private static readonly Dictionary<int, decimal> _paytable = new() {
        { 1, 10m }, // 3 of symbol 1 pays 10x
        { 2, 5m },  // 3 of symbol 2 pays 5x
        { 3, 2m },  // 3 of symbol 3 pays 2x
        { 4, 1m },  // 3 of symbol 4 pays 1x
        { 5, 0.5m } // 3 of symbol 5 pays 0.5x
    };
    
    public SlotGameEngine(IRngService rngService, IVaultService vaultService, IBrainService brainService, IPromotionService promotionService, IJackpotService jackpotService, IRealTimeService realTimeService, IServiceScopeFactory scopeFactory) 
        : base(rngService, vaultService, brainService, promotionService, jackpotService, realTimeService, scopeFactory) {
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

                // 1. ASK THE BRAIN (The new Logic)
                var decision = BrainService.DecideOutcome(session.UserId, session.GameId, lastBet.Amount);
                
                int[] resultSymbols;
                decimal winAmount;

                // 2. REVERSE ENGINEERING (The CMS Logic)
                if (decision.TargetWinAmount > 0) {
                    // Find symbols matching target win
                    decimal targetMultiplier = decision.TargetWinAmount / lastBet.Amount;
                    resultSymbols = GeneratePatternForWin(targetMultiplier);
                    winAmount = CalculateWin(resultSymbols, lastBet.Amount);
                }
                else if (decision.IsNearMiss) {
                    // Generate Teaser
                    resultSymbols = new int[] { 1, 1, 2 }; // Hardcoded Near Miss for MVP
                    winAmount = 0;
                }
                else {
                    // Brain said "Loss" or "Random Loss"
                    // Generate a losing combination
                    resultSymbols = GenerateLosingPattern(session.Seed, roundNumber);
                    winAmount = 0;
                }

                // 3. VAULT EXECUTION (Credit Win)
                if (winAmount > 0) {
                    // We assume VaultService.ProcessWin just credits it. 
                    // Solvency check was done in BrainService.DecideOutcome.
                    VaultService.ProcessWin(session.UserId, winAmount, repo);
                    
                    // Update Brain Statistics
                    BrainService.UpdateProfile(session.UserId, lastBet.Amount, winAmount);
                } else {
                     BrainService.UpdateProfile(session.UserId, lastBet.Amount, 0);
                }
                
                // Track Tournament Win
                PromotionService.ProcessWinActivity(session.UserId, winAmount, repo);

                // Apply Jackpot (Independent of Brain? Or controlled? Let's keep it random for now)
                var jackpotResult = await JackpotService.CheckJackpotTrigger(session.GameId, session.Seed, roundNumber, repo);
                if (jackpotResult.Triggered) {
                    winAmount += jackpotResult.WinAmount;
                    VaultService.ProcessWin(session.UserId, jackpotResult.WinAmount, repo);
                }

                var round = new GameRound {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    RoundNumber = roundNumber,
                    InputData = JsonSerializer.Serialize(new { Decision = decision.DecisionType }),
                    RandomResult = JsonSerializer.Serialize(new { Symbols = resultSymbols }),
                    TotalBetAmount = lastBet.Amount,
                    TotalWinAmount = winAmount,
                    ExecutedAt = DateTime.UtcNow,
                    DecisionType = decision.DecisionType,
                    TargetWinAmount = decision.TargetWinAmount
                };

                repo.SaveRound(round);

                // Link bet to round
                lastBet.GameRoundId = round.Id;
                repo.UpdateBet(lastBet);
                
                var outcome = new Outcome {
                    Id = Guid.NewGuid(),
                    GameRoundId = round.Id,
                    ResultJson = JsonSerializer.Serialize(new { Symbols = resultSymbols, Win = winAmount }),
                    WinAmount = winAmount
                };
                repo.SaveOutcome(outcome);
                
                transaction.Commit();

                // Notify UI
                await RealTimeService.NotifyGameUpdate(session.UserId, new { 
                    SessionId = sessionId, 
                    Game = "Slot", 
                    Result = resultSymbols, 
                    Win = winAmount,
                    RoundId = round.Id
                });

                return round;
            }
            catch {
                transaction.Rollback();
                throw;
            }
        });
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) {
        return await Task.Run(() => ExecuteScoped(repo => repo.GetOutcome(roundId) 
               ?? new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId, ResultJson = "{}" }));
    }

    private int[] GeneratePatternForWin(decimal targetMultiplier) {
        // Find best match in Paytable
        foreach (var entry in _paytable) {
            if (entry.Value == targetMultiplier) {
                return new int[] { entry.Key, entry.Key, entry.Key };
            }
        }
        
        // If no exact match, fallback to lowest win (Symbol 5 = 0.5x) or close match
        return new int[] { 5, 5, 5 };
    }

    private int[] GenerateLosingPattern(int seed, int round) {
        // Just return mismatch
        return new int[] { 1, 2, 3 };
    }

    private decimal CalculateWin(int[] symbols, decimal betAmount) {
        if (symbols[0] == symbols[1] && symbols[1] == symbols[2]) {
            if (_paytable.TryGetValue(symbols[0], out decimal multiplier)) {
                return betAmount * multiplier;
            }
        }
        return 0;
    }
}
