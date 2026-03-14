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
    private readonly Guid _gameId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // Symbols: 1:Lemon, 2:Cherry, 3:Orange, 4:Plum, 5:Watermelon, 6:Apple, 7:Star, 8:TNT, 9:Nuclear, 10:Supernova, 12:Golden Apple
    private static readonly Dictionary<int, decimal[]> Paytable = new() {
        { 1, new[] { 0.5m, 1.2m, 3.5m, 9.0m, 22.0m } }, 
        { 2, new[] { 0.5m, 1.2m, 3.5m, 9.0m, 22.0m } },
        { 3, new[] { 0.7m, 1.8m, 4.5m, 11.0m, 28.0m } },
        { 4, new[] { 0.9m, 2.2m, 6.5m, 16.0m, 45.0m } },
        { 5, new[] { 1.2m, 3.5m, 11.0m, 35.0m, 75.0m } },
        { 6, new[] { 3.0m, 8.0m, 28.0m, 75.0m, 160.0m } },
        { 7, new[] { 7.0m, 22.0m, 75.0m, 280.0m, 1100.0m } },
        { 12, new[] { 18.0m, 65.0m, 220.0m, 550.0m, 1100.0m } } 
    };

    public class FruitBlastState {
        public int[][] Grid { get; set; } = new int[Rows][];
        public int JuiceMeter { get; set; } = 0;
        public decimal TotalMultiplier { get; set; } = 1.0m;
        public List<AvalancheStep> History { get; set; } = new();
        public decimal CurrentRoundWin { get; set; } = 0;
        public bool IsFinished { get; set; } = false;
        public decimal Denomination { get; set; } = 0.01m;
        public decimal JuicePotValue { get; set; }
        public bool IsMeltdownTriggered { get; set; }
        public bool IsMegaFruitTriggered { get; set; }
        public int LifetimeExplosions { get; set; }
        public decimal MaxWinCap { get; set; }

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
        public bool MeltdownActive { get; set; }
        public MegaFruit? MegaFruit { get; set; }
    }

    public class Point { public int R { get; set; } public int C { get; set; } }
    public class BombExplosion { public Point Origin { get; set; } = new(); public List<Point> Affected { get; set; } = new(); public int Type { get; set; } }
    public class MegaFruit { public Point TopLeft { get; set; } = new(); public int SymbolId { get; set; } }

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

            decimal denom = 0.01m;
            try {
                var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(bet.BetData);
                if (json.TryGetProperty("Denomination", out var d) || json.TryGetProperty("denomination", out d)) 
                    denom = d.GetDecimal();
            } catch {}

            var directive = BrainService.GetNextDirective(session.UserId, _gameId, bet.Amount, repo);
            var playerProfile = repo.GetPlayerProfile(session.UserId);
            var juicePot = repo.GetOrCreateLocalJackpot(_gameId);
            if (juicePot.Name != "The Juice Pot") {
                juicePot.Name = "The Juice Pot";
                if (juicePot.CurrentValue < 50) juicePot.CurrentValue = 50;
                repo.UpdateJackpot(juicePot);
            }

            var state = new FruitBlastState { 
                Denomination = denom, 
                JuicePotValue = juicePot.CurrentValue,
                LifetimeExplosions = playerProfile?.FruitBlastLifetimeExplosions ?? 0
            };
            
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int baseNonce = roundNum * 1000;

            FillGrid(state.Grid, session, baseNonce, directive);

            int avalancheCount = 0;
            bool continueAvalanche = true;
            bool meltdownAwarded = false;

            while (continueAvalanche && avalancheCount < 20) {
                var step = new AvalancheStep {
                    GridBefore = CopyGrid(state.Grid),
                    JuiceMeterValue = state.JuiceMeter
                };

                // --- Level 3: Vitamin Overload Check ---
                if (state.LifetimeExplosions >= 5000 && !state.IsMegaFruitTriggered) {
                    state.IsMegaFruitTriggered = true;
                    step.MegaFruit = new MegaFruit { TopLeft = new Point { R = 1, C = 1 }, SymbolId = 12 }; // Golden Apple Mega
                    // Transform 3x3 area
                    for(int r=1; r<=3; r++) for(int c=1; c<=3; c++) state.Grid[r][c] = 12;
                    // Reset lifetime counter (or keep going?) - reset for now
                    if (playerProfile != null) playerProfile.FruitBlastLifetimeExplosions = 0;
                }

                var clusters = FindClusters(state.Grid);
                if (clusters.Any()) {
                    step.WinningClusters = clusters.SelectMany(c => c).ToList();
                    decimal stepWin = CalculateClusterWin(state.Grid, clusters, bet.Amount, state.JuiceMeter);
                    state.CurrentRoundWin += stepWin;
                    step.WinAmount = stepWin;
                    
                    int explodedCount = step.WinningClusters.Count;
                    state.JuiceMeter += explodedCount;
                    state.LifetimeExplosions += explodedCount;
                    if (playerProfile != null) playerProfile.FruitBlastLifetimeExplosions += explodedCount;

                    // Update Juice Pot Contribution (0.1% per exploded fruit)
                    decimal contribution = bet.Amount * 0.001m * explodedCount;
                    juicePot.CurrentValue += contribution;
                    state.JuicePotValue = juicePot.CurrentValue;

                    step.AffectedColumns.AddRange(step.WinningClusters.Select(p => p.C).Distinct());
                }

                var explosions = ProcessBombs(state.Grid, session, baseNonce + avalancheCount + 50);
                if (explosions.Any()) {
                    step.Explosions = explosions;
                    foreach (var exp in explosions) {
                        foreach (var p in exp.Affected) state.Grid[p.R][p.C] = 0;
                        step.AffectedColumns.AddRange(exp.Affected.Select(p => p.C).Distinct());
                        
                        int affectedCount = exp.Affected.Count;
                        state.JuiceMeter += affectedCount;
                        state.LifetimeExplosions += affectedCount;
                        if (playerProfile != null) playerProfile.FruitBlastLifetimeExplosions += affectedCount;

                        decimal contribution = bet.Amount * 0.001m * affectedCount;
                        juicePot.CurrentValue += contribution;
                        state.JuicePotValue = juicePot.CurrentValue;

                        if (exp.Type == 10) {
                             state.TotalMultiplier *= 2; 
                             if (state.TotalMultiplier > 100) state.TotalMultiplier = 100; 
                        }
                    }
                }

                // --- Level 1 & 2: Juice Pot & Fruit Meltdown Trigger ---
                if (state.JuiceMeter >= 200 && !meltdownAwarded) {
                    state.IsMeltdownTriggered = true;
                    step.MeltdownActive = true;
                    meltdownAwarded = true;
                    
                    // Add Juice Pot to win
                    state.CurrentRoundWin += juicePot.CurrentValue;
                    
                    // Trigger Meltdown transformation
                    for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) state.Grid[r][c] = 12; 
                    
                    // Reset Juice Pot to seed value
                    juicePot.CurrentValue = 50.0m;
                    state.JuicePotValue = juicePot.CurrentValue;
                }

                step.AffectedColumns = step.AffectedColumns.Distinct().OrderBy(c => c).ToList();

                if (!clusters.Any() && !explosions.Any() && !step.MeltdownActive) {
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

            // HARD CAP: Max win 10,000x bet
            decimal maxWinAllowed = bet.Amount * 10000;
            if (totalWin > maxWinAllowed) {
                totalWin = maxWinAllowed;
                state.MaxWinCap = maxWinAllowed;
            }

            // SMART VAULT PROTECTION: Bypassed for simulation users
            var user = repo.GetUser(session.UserId);
            bool isSimUser = user?.Username?.StartsWith("Sim_") ?? false;

            if (totalWin > 0 && !isSimUser) {
                bool isRandom = directive.DecisionType == "Random";
                if (!await VaultService.CanAffordWinAsync(session.UserId, _gameId, totalWin, repo, strictShadowCheck: !isRandom)) {
                    var game = repo.GetGame(_gameId);
                    decimal poolAffordable = (game?.PoolBalance ?? 0) * 0.9m; 
                    decimal fallback = Math.Max(bet.Amount, poolAffordable);
                    if (totalWin > fallback) totalWin = Math.Round(fallback, 2);
                }
            }

            if (totalWin > 0) {
                await VaultService.ProcessWinAsync(session.UserId, totalWin, repo);
                repo.UpdateGamePoolBalance(_gameId, -totalWin);
            }

            if (playerProfile != null) repo.UpdatePlayerProfile(playerProfile);
            repo.UpdateJackpot(juicePot);
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
        if (directive.DecisionType != "Random" && directive.TargetWinAmount > 0) {
            var clusters = FindClusters(grid);
            if (!clusters.Any()) ForceCluster(grid, session, nonce);
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
        for (int r = startR - 1; r <= startR; r++)
            for (int c = startC - 1; c <= startC + 1; c++)
                grid[r][c] = symbol;
    }

    private int GetWeightedSymbol(string serverSeed, string clientSeed, int nonce, BrainDirective directive) {
        int val = RngService.GetNextInt(serverSeed, clientSeed, nonce, 1, 1001);
        int boost = (directive.DecisionType == "RetentionHook") ? 20 : 0;

        if (val <= 970 - boost) {
            int fVal = RngService.GetNextInt(serverSeed, clientSeed, nonce + 10000, 1, 101);
            if (fVal <= 25) return 1; // Lemon 25%
            if (fVal <= 50) return 2; // Cherry 25%
            if (fVal <= 70) return 3; // Orange 20%
            if (fVal <= 85) return 4; // Plum 15%
            return 5; // Watermelon 15%
        }
        if (val <= 975) return 6; // Apple
        if (val <= 982) return 7; // Star
        if (val <= 992 + (boost/2)) return 8; // TNT
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
                if (!visited[r, c] && grid[r][c] > 0 && grid[r][c] <= 7 || grid[r][c] == 12) {
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
        if (juiceMeter >= 200) juiceMultiplier = 12.0m; 
        else if (juiceMeter >= 150) juiceMultiplier = 8.0m; 
        else if (juiceMeter >= 100) juiceMultiplier = 5.0m;
        else if (juiceMeter >= 50) juiceMultiplier = 2.0m;

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
            var juicePot = repo.GetOrCreateLocalJackpot(_gameId);
            
            if (round == null) {
                var session = repo.GetSession(sessionId);
                var profile = session != null ? repo.GetPlayerProfile(session.UserId) : null;
                return Task.FromResult<object?>(new FruitBlastState { 
                    JuicePotValue = juicePot.CurrentValue,
                    LifetimeExplosions = profile?.FruitBlastLifetimeExplosions ?? 0
                });
            }
            
            var state = JsonSerializer.Deserialize<FruitBlastState>(round.RandomResult);
            if (state != null) {
                state.JuicePotValue = juicePot.CurrentValue; // Ensure UI sees most current value
            }
            return Task.FromResult<object?>(state);
        });
    }
}
