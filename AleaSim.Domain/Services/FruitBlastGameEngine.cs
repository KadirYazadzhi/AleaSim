using System.Collections.Concurrent;
using System.Text.Json;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace AleaSim.Domain.Services;

public class FruitBlastGameEngine : BaseGameEngine {
    private readonly IRedisCacheService _cache;
    private const int Rows = 5;
    private const int Cols = 6;
    private const int MinCluster = 8;

    // Symbols: 1:Lemon, 2:Cherry, 3:Orange, 4:Plum, 5:Watermelon, 6:Apple, 7:Star
    // Special: 8:Small Bomb (TNT), 9:Nuclear Bomb, 10:Supernova Bomb
    private static readonly int[] BaseStrip = { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 7, 8, 8, 9, 10 };

    private static readonly Dictionary<int, decimal[]> Paytable = new() {
        { 1, new[] { 0.2m, 0.4m, 0.8m, 1.5m, 3.0m } }, // 8, 10, 12, 15, 20+
        { 2, new[] { 0.2m, 0.4m, 0.8m, 1.5m, 3.0m } },
        { 3, new[] { 0.4m, 0.8m, 1.5m, 3.0m, 6.0m } },
        { 4, new[] { 0.4m, 0.8m, 1.5m, 3.0m, 6.0m } },
        { 5, new[] { 0.8m, 1.5m, 3.0m, 6.0m, 12.0m } },
        { 6, new[] { 0.8m, 1.5m, 3.0m, 6.0m, 12.0m } },
        { 7, new[] { 2.0m, 5.0m, 15.0m, 30.0m, 100.0m } }
    };

    public class FruitBlastState {
        public int[][] Grid { get; set; } = new int[Rows][];
        public int JuiceMeter { get; set; } = 0;
        public decimal TotalMultiplier { get; set; } = 1.0m;
        public List<AvalancheStep> History { get; set; } = new();
        public decimal CurrentRoundWin { get; set; } = 0;
        public bool IsFinished { get; set; } = false;

        public FruitBlastState() {
            for (int r = 0; r < Rows; r++) Grid[r] = new int[Cols];
        }
    }

    public class AvalancheStep {
        public int[][] GridBefore { get; set; } = Array.Empty<int[]>();
        public List<Point> WinningClusters { get; set; } = new();
        public List<BombExplosion> Explosions { get; set; } = new();
        public decimal WinAmount { get; set; }
        public int JuiceMeterValue { get; set; }
    }

    public class Point { public int R { get; set; } public int C { get; set; } }
    public class BombExplosion { public Point Origin { get; set; } = new(); public List<Point> Affected { get; set; } = new(); public int Type { get; set; } }

    public FruitBlastGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, IRedisCacheService cache, ILockService lockService)
        : base(rng, vault, brain, promo, jackpot, realTime, scope, lockService) {
        _cache = cache;
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");

            var bet = repo.GetLastBet(sessionId);
            if (bet == null) throw new Exception("Bet not found");

            // 1. Ask the Brain for a directive
            var directive = BrainService.GetNextDirective(session.UserId, session.GameId, bet.Amount, repo);
            
            var state = new FruitBlastState();
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int baseNonce = roundNum * 1000;

            // Initial Grid Fill
            FillGrid(state.Grid, session, baseNonce);

            // Avalanche Logic (Recursive/Loop)
            int avalancheCount = 0;
            bool continueAvalanche = true;

            while (continueAvalanche && avalancheCount < 15) { // Capped at 15 steps for better pacing
                var step = new AvalancheStep {
                    GridBefore = CopyGrid(state.Grid),
                    JuiceMeterValue = state.JuiceMeter
                };

                // Find Wins
                var clusters = FindClusters(state.Grid);
                if (clusters.Any()) {
                    step.WinningClusters = clusters.SelectMany(c => c).ToList();
                    decimal stepWin = CalculateClusterWin(state.Grid, clusters, bet.Amount, state.JuiceMeter);
                    state.CurrentRoundWin += stepWin;
                    step.WinAmount = stepWin;
                    state.JuiceMeter += clusters.Count; // Increment meter by number of clusters
                }

                // Find Bombs (TNT, Nuclear, Supernova)
                var explosions = ProcessBombs(state.Grid, session, baseNonce + avalancheCount + 50);
                if (explosions.Any()) {
                    step.Explosions = explosions;
                    // Apply Explosions (Remove symbols)
                    foreach (var exp in explosions) {
                        foreach (var p in exp.Affected) state.Grid[p.R][p.C] = 0;
                        if (exp.Type == 10) {
                             state.TotalMultiplier *= 2; // Supernova doubles multiplier
                             if (state.TotalMultiplier > 100) state.TotalMultiplier = 100; // Cap to 100x for sanity
                        }
                    }
                }

                // If no wins and no explosions, stop
                if (!clusters.Any() && !explosions.Any()) {
                    continueAvalanche = false;
                } else {
                    // Remove symbols (if not already by bombs)
                    foreach (var cluster in clusters) {
                        foreach (var p in cluster) state.Grid[p.R][p.C] = 0;
                    }

                    // Fall and Fill
                    ApplyGravity(state.Grid);
                    FillMissing(state.Grid, session, baseNonce + 100 + avalancheCount);
                    
                    state.History.Add(step);
                    avalancheCount++;
                }
            }

            state.IsFinished = true;
            decimal totalWin = state.CurrentRoundWin * state.TotalMultiplier;

            // 2. Validate with Vault (Can we afford this?)
            if (totalWin > 0 && !await VaultService.CanAffordWinAsync(session.UserId, session.GameId, totalWin, repo)) {
                // FORCE LOSS/RE-ROLL (Near Miss logic)
                totalWin = 0;
                state.CurrentRoundWin = 0;
            }

            // 3. Process Win
            if (totalWin > 0) {
                await VaultService.ProcessWinAsync(session.UserId, totalWin, repo);
                repo.UpdateGamePoolBalance(session.GameId, -totalWin);
            }

            // 4. Update Profile (Brain)
            BrainService.UpdateProfile(session.UserId, 0, totalWin, repo);

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                RoundNumber = roundNum,
                TotalBetAmount = bet.Amount,
                TotalWinAmount = totalWin,
                DecisionType = directive.DecisionType,
                TargetWinAmount = directive.TargetWinAmount,
                RandomResult = JsonSerializer.Serialize(state),
                ExecutedAt = DateTime.UtcNow,
                ServerSeed = session.ServerSeed,
                ClientSeed = session.ClientSeed,
                Nonce = baseNonce
            };

            repo.SaveRound(round);
            repo.UpdateSession(session);

            return round;
        });
    }

    private void FillGrid(int[][] grid, GameSession session, int nonce) {
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                grid[r][c] = GetWeightedSymbol(session.ServerSeed, session.ClientSeed, nonce + (r * Cols + c));
            }
        }
    }

    private void FillMissing(int[][] grid, GameSession session, int nonce) {
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                if (grid[r][c] == 0) {
                    grid[r][c] = GetWeightedSymbol(session.ServerSeed, session.ClientSeed, nonce + (r * Cols + c));
                }
            }
        }
    }

    private int GetWeightedSymbol(string serverSeed, string clientSeed, int nonce) {
        int val = RngService.GetNextInt(serverSeed, clientSeed, nonce, 1, 101);
        // 92% Fruits (1-7), 6% TNT (8), 1.5% Nuclear (9), 0.5% Supernova (10)
        if (val <= 92) return RngService.GetNextInt(serverSeed, clientSeed, nonce + 1000, 1, 8); 
        if (val <= 98) return 8; 
        if (val <= 99) return 9;
        return 10;
    }

    private int[][] CopyGrid(int[][] grid) {
        var copy = new int[Rows][];
        for (int r = 0; r < Rows; r++) {
            copy[r] = new int[Cols];
            Array.Copy(grid[r], copy[r], Cols);
        }
        return copy;
    }

    private List<List<Point>> FindClusters(int[][] grid) {
        var clusters = new List<List<Point>>();
        var visited = new bool[Rows, Cols];

        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                if (!visited[r, c] && grid[r][c] > 0 && grid[r][c] <= 7) {
                    var cluster = new List<Point>();
                    DFS(grid, r, c, grid[r][c], visited, cluster);
                    if (cluster.Count >= MinCluster) {
                        clusters.Add(cluster);
                    }
                }
            }
        }
        return clusters;
    }

    private void DFS(int[][] grid, int r, int c, int symbol, bool[,] visited, List<Point> cluster) {
        if (r < 0 || r >= Rows || c < 0 || c >= Cols || visited[r, c] || grid[r][c] != symbol) return;

        visited[r, c] = true;
        cluster.Add(new Point { R = r, C = c });

        DFS(grid, r - 1, c, symbol, visited, cluster);
        DFS(grid, r + 1, c, symbol, visited, cluster);
        DFS(grid, r, c - 1, symbol, visited, cluster);
        DFS(grid, r, c + 1, symbol, visited, cluster);
    }

    private decimal CalculateClusterWin(int[][] grid, List<List<Point>> clusters, decimal bet, int juiceMeter) {
        decimal totalWin = 0;
        decimal juiceMultiplier = 1.0m;
        if (juiceMeter >= 20) juiceMultiplier = 10.0m;
        else if (juiceMeter >= 10) juiceMultiplier = 3.0m;
        else if (juiceMeter >= 5) juiceMultiplier = 2.0m;

        foreach (var cluster in clusters) {
            int symbol = grid[cluster[0].R][cluster[0].C]; 
            if (Paytable.TryGetValue(symbol, out var multipliers)) {
                int count = cluster.Count;
                decimal mult = 0;
                if (count >= 20) mult = multipliers[4];
                else if (count >= 15) mult = multipliers[3];
                else if (count >= 12) mult = multipliers[2];
                else if (count >= 10) mult = multipliers[1];
                else if (count >= 8) mult = multipliers[0];

                totalWin += (bet / 10) * mult * juiceMultiplier; // Scaled win
            }
        }
        return totalWin;
    }

    private void ApplyGravity(int[][] grid) {
        for (int c = 0; c < Cols; c++) {
            int emptySpot = Rows - 1;
            for (int r = Rows - 1; r >= 0; r--) {
                if (grid[r][c] != 0) {
                    if (r != emptySpot) {
                        grid[emptySpot][c] = grid[r][c];
                        grid[r][c] = 0;
                    }
                    emptySpot--;
                }
            }
        }
    }

    private List<BombExplosion> ProcessBombs(int[][] grid, GameSession session, int nonce) {
        var explosions = new List<BombExplosion>();
        var bombPositions = new List<Point>();

        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                if (grid[r][c] >= 8 && grid[r][c] <= 10) bombPositions.Add(new Point { R = r, C = c });
            }
        }

        foreach (var bomb in bombPositions) {
            int type = grid[bomb.R][bomb.C];
            var affected = new List<Point>();

            if (type == 8) { // Small Bomb (3x3)
                for (int dr = -1; dr <= 1; dr++) {
                    for (int dc = -1; dc <= 1; dc++) {
                        int nr = bomb.R + dr;
                        int nc = bomb.C + dc;
                        if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols) affected.Add(new Point { R = nr, C = nc });
                    }
                }
            } else if (type == 9) { // Nuclear Bomb (Needs 2+)
                // Connect all nuclear bombs in cross pattern
                for (int r = 0; r < Rows; r++) affected.Add(new Point { R = r, C = bomb.C });
                for (int c = 0; c < Cols; c++) affected.Add(new Point { R = bomb.R, C = c });
            } else if (type == 10) { // Supernova (The Grid)
                for (int r = 0; r < Rows; r++) {
                    for (int c = 0; c < Cols; c++) affected.Add(new Point { R = r, C = c });
                }
            }

            explosions.Add(new BombExplosion { Origin = bomb, Affected = affected, Type = type });
        }

        return explosions;
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) {
        return await Task.FromResult(new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId });
    }

    public override async Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) {
        await Task.CompletedTask;
    }

    public override async Task<object?> GetCurrentState(Guid sessionId) {
        return await Task.FromResult(new FruitBlastState());
    }
}
