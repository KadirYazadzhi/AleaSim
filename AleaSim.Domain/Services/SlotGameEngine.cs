using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class SlotGameEngine : BaseGameEngine {
    // 5 Reels x 4 Rows
    private const int Rows = 4;
    private const int Cols = 5;

    // Symbol IDs
    // 1-4: Low Pay, 5-7: High Pay, 8: Wild, 9: Scatter/Bonus
    private readonly int[] _symbols = { 1, 2, 3, 4, 5, 6, 7 };

    // 15 Paylines (Standard definitions)
    // Format: [Row_Col0, Row_Col1, Row_Col2, Row_Col3, Row_Col4]
    private static readonly int[][] _paylines = new[] {
        new[] { 1, 1, 1, 1, 1 }, // Line 1: Center Row
        new[] { 0, 0, 0, 0, 0 }, // Line 2: Top Row
        new[] { 2, 2, 2, 2, 2 }, // Line 3: Bottom Row
        new[] { 3, 3, 3, 3, 3 }, // Line 4: Row 4
        new[] { 0, 1, 2, 1, 0 }, // Line 5: V shape
        new[] { 2, 1, 0, 1, 2 }, // Line 6: Inverted V
        new[] { 1, 0, 0, 0, 1 }, // Line 7
        new[] { 1, 2, 2, 2, 1 }, // Line 8
        new[] { 0, 0, 1, 2, 2 }, // Line 9
        new[] { 2, 2, 1, 0, 0 }, // Line 10
        new[] { 0, 1, 1, 1, 0 }, // Line 11
        new[] { 2, 1, 1, 1, 2 }, // Line 12
        new[] { 0, 1, 0, 1, 0 }, // Line 13
        new[] { 2, 1, 2, 1, 2 }, // Line 14
        new[] { 1, 1, 0, 1, 1 }  // Line 15
    };

    // Paytable: Symbol -> { Count -> Multiplier }
    private static readonly Dictionary<int, Dictionary<int, decimal>> _paytable = new() {
        { 1, new() { {3, 0.5m}, {4, 2m}, {5, 5m} } },   // Cherry
        { 2, new() { {3, 0.5m}, {4, 2m}, {5, 5m} } },   // Lemon
        { 3, new() { {3, 1m}, {4, 5m}, {5, 10m} } },    // Orange
        { 4, new() { {3, 1m}, {4, 5m}, {5, 10m} } },    // Plum
        { 5, new() { {3, 2m}, {4, 10m}, {5, 20m} } },   // Bell
        { 6, new() { {3, 5m}, {4, 25m}, {5, 50m} } },   // Bar
        { 7, new() { {3, 10m}, {4, 50m}, {5, 100m} } }  // Seven
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

                // Assuming bet per line logic. 
                // TotalBet = BetPerLine * 15.
                // We use TotalBet for calculations.
                decimal betPerLine = lastBet.Amount / _paylines.Length;

                // 1. ASK THE BRAIN
                var decision = BrainService.DecideOutcome(session.UserId, session.GameId, lastBet.Amount);
                
                int[,] grid = new int[Rows, Cols];
                decimal winAmount = 0;

                // 2. REVERSE ENGINEERING (Multi-Line Solver)
                if (decision.TargetWinAmount > 0) {
                    // Try to construct grid for target win
                    grid = ConstructGridForWin(decision.TargetWinAmount, betPerLine, out winAmount);
                }
                else if (decision.IsNearMiss) {
                    grid = ConstructNearMiss();
                    winAmount = 0;
                }
                else {
                    grid = ConstructLosingGrid();
                    winAmount = 0;
                }

                // 3. VAULT EXECUTION
                if (winAmount > 0) {
                    VaultService.ProcessWin(session.UserId, winAmount, repo);
                    BrainService.UpdateProfile(session.UserId, lastBet.Amount, winAmount);
                } else {
                     BrainService.UpdateProfile(session.UserId, lastBet.Amount, 0);
                }
                
                PromotionService.ProcessWinActivity(session.UserId, winAmount, repo);

                // Jackpot Check (Overlay)
                var jackpotResult = await JackpotService.CheckJackpotTrigger(session.GameId, session.Seed, repo.GetRoundCount(sessionId), repo);
                if (jackpotResult.Triggered) {
                    winAmount += jackpotResult.WinAmount;
                    VaultService.ProcessWin(session.UserId, jackpotResult.WinAmount, repo);
                }

                var round = new GameRound {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    RoundNumber = repo.GetRoundCount(sessionId) + 1,
                    InputData = JsonSerializer.Serialize(new { Decision = decision.DecisionType }),
                    RandomResult = JsonSerializer.Serialize(new { Grid = FlattenGrid(grid) }),
                    TotalBetAmount = lastBet.Amount,
                    TotalWinAmount = winAmount,
                    ExecutedAt = DateTime.UtcNow,
                    DecisionType = decision.DecisionType,
                    TargetWinAmount = decision.TargetWinAmount
                };

                repo.SaveRound(round);
                lastBet.GameRoundId = round.Id;
                repo.UpdateBet(lastBet);
                
                var outcome = new Outcome {
                    Id = Guid.NewGuid(),
                    GameRoundId = round.Id,
                    ResultJson = JsonSerializer.Serialize(new { Grid = FlattenGrid(grid), Win = winAmount }),
                    WinAmount = winAmount
                };
                repo.SaveOutcome(outcome);
                
                transaction.Commit();

                await RealTimeService.NotifyGameUpdate(session.UserId, new { 
                    SessionId = sessionId, 
                    Game = "Slot", 
                    Grid = FlattenGrid(grid), 
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

    // --- CONSTRUCTION LOGIC ---

    private int[,] ConstructGridForWin(decimal targetWin, decimal betPerLine, out decimal actualWin) {
        // Simplified Solver: Find ONE line that satisfies the win (or close to it)
        // Advanced: Combine multiple lines. For MVP, we stick to single-line dominance to ensure performance.
        
        decimal targetMultiplier = targetWin / betPerLine;
        int[,] grid = new int[Rows, Cols];
        
        // Fill with junk first
        FillJunk(grid);

        // Find best symbol match
        // Need to find (Symbol, Count) where Multiplier ~= Target
        int bestSymbol = 1;
        int bestCount = 3;
        decimal minDiff = decimal.MaxValue;

        foreach (var sym in _paytable) {
            foreach (var pay in sym.Value) {
                decimal diff = Math.Abs(pay.Value - targetMultiplier);
                if (diff < minDiff) {
                    minDiff = diff;
                    bestSymbol = sym.Key;
                    bestCount = pay.Key;
                }
            }
        }

        // Set Line 1 (Middle Row) to this combination
        int[] line1 = _paylines[0];
        for (int i = 0; i < bestCount; i++) {
            grid[line1[i], i] = bestSymbol;
        }
        // Ensure next symbol breaks the line (if count < 5)
        if (bestCount < 5) {
            grid[line1[bestCount], bestCount] = (bestSymbol == 1) ? 2 : 1;
        }

        // Recalculate actual win (to account for accidental wins)
        actualWin = CalculateTotalWin(grid, betPerLine);
        return grid;
    }

    private int[,] ConstructNearMiss() {
        int[,] grid = new int[Rows, Cols];
        FillJunk(grid);
        
        // Put 4 Sevens on Line 1, but block the 5th
        int[] line1 = _paylines[0];
        for (int i = 0; i < 4; i++) grid[line1[i], i] = 7;
        grid[line1[4], 4] = 1; // Blocker

        return grid;
    }

    private int[,] ConstructLosingGrid() {
        int[,] grid = new int[Rows, Cols];
        FillJunk(grid);
        // Verify 0 win
        while (CalculateTotalWin(grid, 1) > 0) {
            FillJunk(grid); // Retry (Lazy approach, but effective for sparse wins)
        }
        return grid;
    }

    private void FillJunk(int[,] grid) {
        var rnd = new Random();
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                grid[r, c] = _symbols[rnd.Next(_symbols.Length)];
            }
        }
    }

    private decimal CalculateTotalWin(int[,] grid, decimal betPerLine) {
        decimal totalWin = 0;
        foreach (var line in _paylines) {
            int firstSym = grid[line[0], 0];
            int count = 1;
            for (int c = 1; c < Cols; c++) {
                if (grid[line[c], c] == firstSym || firstSym == 8) { // 8 is Wild
                    count++;
                } else {
                    break;
                }
            }

            if (count >= 3) {
                if (_paytable.ContainsKey(firstSym) && _paytable[firstSym].ContainsKey(count)) {
                    totalWin += _paytable[firstSym][count] * betPerLine;
                }
            }
        }
        return totalWin;
    }

    private int[][] FlattenGrid(int[,] grid) {
        int[][] flat = new int[Rows][];
        for (int r = 0; r < Rows; r++) {
            flat[r] = new int[Cols];
            for (int c = 0; c < Cols; c++) {
                flat[r][c] = grid[r, c];
            }
        }
        return flat;
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) {
        return await Task.Run(() => ExecuteScoped(repo => repo.GetOutcome(roundId) 
               ?? new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId, ResultJson = "{}" }));
    }
}