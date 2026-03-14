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
    private const int MinCluster = 5;

    // Symbols: 1:Lemon, 2:Cherry, 3:Orange, 4:Plum, 5:Watermelon, 6:Apple, 7:Star
    // High multipliers to reach 96%+ RTP with current cluster logic
    private static readonly Dictionary<int, decimal[]> Paytable = new() {
        { 1, new[] { 0.4m, 1.0m, 2.5m, 6.0m, 20.0m } }, 
        { 2, new[] { 0.4m, 1.0m, 2.5m, 6.0m, 20.0m } },
        { 3, new[] { 0.6m, 1.5m, 4.0m, 10.0m, 35.0m } },
        { 4, new[] { 0.8m, 2.0m, 6.0m, 15.0m, 50.0m } },
        { 5, new[] { 1.5m, 4.0m, 12.0m, 30.0m, 100.0m } },
        { 6, new[] { 2.5m, 7.5m, 25.0m, 60.0m, 250.0m } },
        { 7, new[] { 10.0m, 40.0m, 150.0m, 500.0m, 2500.0m } }
    };

    public class FruitBlastState {
        public int[][] Grid { get; set; } = new int[Rows][];
        public int JuiceMeter { get; set; } = 0;
        public decimal TotalMultiplier { get; set; } = 1.0m;
        public List<AvalancheStep> History { get; set; } = new();
        public decimal CurrentRoundWin { get; set; } = 0;
        public bool IsFinished { get; set; } = false;
        public decimal Denomination { get; set; } = 0.01m;

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
        public List<int> AffectedColumns { get; set; } = new();
    }

    public class Point { public int R { get; set; } public int C { get; set; } }
    public class BombExplosion { public Point Origin { get; set; } = new(); public List<Point> Affected { get; set; } = new(); public int Type { get; set; } }

    public FruitBlastGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, IRedisCacheService cache, ILockService lockService)
        : base(rng, vault, brain, promo, jackpot, realTime, scope, lockService) {
        _cache = cache;
    }

    public override async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string? betData) {
        decimal denom = 0.01m; 
        try { 
            if (!string.IsNullOrEmpty(betData)) {
                var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(betData);
                if (json.TryGetProperty("Denomination", out var d) || json.TryGetProperty("denomination", out d)) 
                    denom = d.GetDecimal();
            }
        } catch {}
        if (denom <= 0) denom = 0.01m;

        decimal minBet = 10 * denom; 
        if (amount < minBet && amount > 0) throw new Exception($"Minimum bet for this denomination is {minBet:C2}.");

        await base.PlaceBet(userId, sessionId, amount, betData);
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");

            var bet = repo.GetLastBet(sessionId);
            if (bet == null) throw new Exception("Bet not found");

            decimal denom = 0.01m;
            try {
                var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(bet.BetData);
                if (json.TryGetProperty("Denomination", out var d) || json.TryGetProperty("denomination", out d)) 
                    denom = d.GetDecimal();
            } catch {}
            if (denom <= 0) denom = 0.01m;

            var directive = BrainService.GetNextDirective(session.UserId, session.GameId, bet.Amount, repo);
            
            var state = new FruitBlastState { Denomination = denom };
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int baseNonce = roundNum * 1000;

            FillGrid(state.Grid, session, baseNonce, directive);

            int avalancheCount = 0;
            bool continueAvalanche = true;

            while (continueAvalanche && avalancheCount < 15) {
                var step = new AvalancheStep {
                    GridBefore = CopyGrid(state.Grid),
                    JuiceMeterValue = state.JuiceMeter
                };

                var clusters = FindClusters(state.Grid);
                if (clusters.Any()) {
                    step.WinningClusters = clusters.SelectMany(c => c).ToList();
                    decimal stepWin = CalculateClusterWin(state.Grid, clusters, bet.Amount, state.JuiceMeter);
                    state.CurrentRoundWin += stepWin;
                    step.WinAmount = stepWin;
                    state.JuiceMeter += step.WinningClusters.Count; 
                    step.AffectedColumns.AddRange(step.WinningClusters.Select(p => p.C).Distinct());
                }

                var explosions = ProcessBombs(state.Grid, session, baseNonce + avalancheCount + 50);
                if (explosions.Any()) {
                    step.Explosions = explosions;
                    foreach (var exp in explosions) {
                        foreach (var p in exp.Affected) state.Grid[p.R][p.C] = 0;
                        step.AffectedColumns.AddRange(exp.Affected.Select(p => p.C).Distinct());
                        state.JuiceMeter += exp.Affected.Count; 
                        if (exp.Type == 10) {
                             state.TotalMultiplier *= 2; 
                             if (state.TotalMultiplier > 100) state.TotalMultiplier = 100; 
                        }
                    }
                }

                step.AffectedColumns = step.AffectedColumns.Distinct().OrderBy(c => c).ToList();

                if (!clusters.Any() && !explosions.Any()) {
                    continueAvalanche = false;
                } else {
                    foreach (var cluster in clusters) {
                        foreach (var p in cluster) state.Grid[p.R][p.C] = 0;
                    }

                    ApplyGravity(state.Grid);
                    FillMissing(state.Grid, session, baseNonce + 100 + (avalancheCount * 30), directive);
                    
                    state.History.Add(step);
                    avalancheCount++;
                }
            }

            state.IsFinished = true;
            decimal totalWin = Math.Round(state.CurrentRoundWin * state.TotalMultiplier, 2);

            if (totalWin > 0 && !await VaultService.CanAffordWinAsync(session.UserId, session.GameId, totalWin, repo)) {
                totalWin = 0;
                state.CurrentRoundWin = 0;
            }

            if (totalWin > 0) {
                await VaultService.ProcessWinAsync(session.UserId, totalWin, repo);
                repo.UpdateGamePoolBalance(session.GameId, -totalWin);
            }

            BrainService.UpdateProfile(session.UserId, bet.Amount, totalWin, repo);

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

    private void FillGrid(int[][] grid, GameSession session, int nonce, BrainDirective directive) {
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                grid[r][c] = GetWeightedSymbol(session.ServerSeed, session.ClientSeed, nonce + (r * Cols + c), directive);
            }
        }
        
        // If brain wants a win and we have none, force one cluster
        if (directive.DecisionType != "Random" && directive.TargetWinAmount > 0) {
            var clusters = FindClusters(grid);
            if (!clusters.Any()) {
                ForceCluster(grid, session, nonce);
            }
        }
    }

    private void FillMissing(int[][] grid, GameSession session, int nonce, BrainDirective directive) {
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                if (grid[r][c] == 0) {
                    grid[r][c] = GetWeightedSymbol(session.ServerSeed, session.ClientSeed, nonce + (r * Cols + c) + 5000, directive);
                }
            }
        }
    }

    private void ForceCluster(int[][] grid, GameSession session, int nonce) {
        int symbol = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + 555, 1, 5);
        int startR = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + 666, 1, Rows - 1);
        int startC = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + 777, 1, Cols - 1);
        
        // Create 2x3 cluster
        for (int r = startR - 1; r <= startR; r++)
            for (int c = startC - 1; c <= startC + 1; c++)
                grid[r][c] = symbol;
    }

    private int GetWeightedSymbol(string serverSeed, string clientSeed, int nonce, BrainDirective directive) {
        int val = RngService.GetNextInt(serverSeed, clientSeed, nonce, 1, 1001);
        
        // Boost special symbols if brain wants to help
        int boost = (directive.DecisionType == "RetentionHook") ? 20 : 0;

        if (val <= 970 - boost) {
            int fVal = RngService.GetNextInt(serverSeed, clientSeed, nonce + 10000, 1, 101);
            if (fVal <= 35) return 1; // Lemon 35%
            if (fVal <= 65) return 2; // Cherry 30%
            if (fVal <= 85) return 3; // Orange 20%
            if (fVal <= 95) return 4; // Plum 10%
            return 5; // Watermelon 5%
        }
        
        if (val <= 980) return 6; // Apple 1%
        if (val <= 985) return 7; // Star 0.5%
        if (val <= 994 + (boost/2)) return 8; // TNT
        if (val <= 998 + (boost/4)) return 9; // Nuclear
        return 10; // Supernova
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
                    if (cluster.Count >= MinCluster) clusters.Add(cluster);
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
        if (juiceMeter >= 60) juiceMultiplier = 10.0m;
        else if (juiceMeter >= 30) juiceMultiplier = 5.0m;
        else if (juiceMeter >= 10) juiceMultiplier = 2.0m;

        foreach (var cluster in clusters) {
            int symbol = grid[cluster[0].R][cluster[0].C]; 
            if (Paytable.TryGetValue(symbol, out var multipliers)) {
                int count = cluster.Count;
                decimal mult = multipliers[0];
                if (count >= 25) mult = multipliers[4];
                else if (count >= 18) mult = multipliers[3];
                else if (count >= 12) mult = multipliers[2];
                else if (count >= 8) mult = multipliers[1];

                totalWin += bet * mult * juiceMultiplier; 
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
            if (type == 8) { 
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++) {
                        int nr = bomb.R + dr; int nc = bomb.C + dc;
                        if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols) affected.Add(new Point { R = nr, C = nc });
                    }
            } else if (type == 9) { 
                for (int r = 0; r < Rows; r++) affected.Add(new Point { R = r, C = bomb.C });
                for (int c = 0; c < Cols; c++) affected.Add(new Point { R = bomb.R, C = c });
            } else if (type == 10) { 
                for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) affected.Add(new Point { R = r, C = c });
            }
            explosions.Add(new BombExplosion { Origin = bomb, Affected = affected, Type = type });
        }
        return explosions;
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) => await Task.FromResult(new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId });
    public override async Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) => await Task.CompletedTask;
    public override async Task<object?> GetCurrentState(Guid sessionId) {
        return await ExecuteScopedAsync(repo => {
            var round = repo.GetLastRound(sessionId);
            if (round == null) return Task.FromResult<object?>(new FruitBlastState());
            return Task.FromResult<object?>(JsonSerializer.Deserialize<FruitBlastState>(round.RandomResult));
        });
    }
}
