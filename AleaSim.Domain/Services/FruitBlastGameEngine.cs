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
    private readonly Guid _gameId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private readonly FruitBlastConfig _defaultConfig = new();

    // Symbols: 1:Lemon, 2:Cherry, 3:Orange, 4:Plum, 5:Watermelon, 6:Apple, 7:Star, 8:TNT, 9:Nuclear, 10:Supernova, 12:Golden Apple
    private static readonly Dictionary<int, decimal[]> Paytable = new() {
        { 1, new[] { 0.5m, 1.2m, 4.0m, 10.0m, 25.0m } }, 
        { 2, new[] { 0.5m, 1.2m, 4.0m, 10.0m, 25.0m } },
        { 3, new[] { 0.8m, 2.0m, 6.0m, 15.0m, 40.0m } },
        { 4, new[] { 1.0m, 3.0m, 10.0m, 25.0m, 60.0m } },
        { 5, new[] { 1.5m, 5.0m, 15.0m, 40.0m, 100.0m } },
        { 6, new[] { 4.0m, 10.0m, 30.0m, 100.0m, 250.0m } },
        { 7, new[] { 10.0m, 30.0m, 100.0m, 400.0m, 1500.0m } },
        { 12, new[] { 25.0m, 100.0m, 300.0m, 800.0m, 2000.0m } } 
    };

    public class FruitBlastState {
        public int[][] Grid { get; set; }
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

        public FruitBlastState(int rows, int cols) {
            Grid = new int[rows][];
            for (int r = 0; r < rows; r++) Grid[r] = new int[cols];
        }
        public FruitBlastState() : this(5, 6) { }
    }

    private FruitBlastConfig LoadConfig(Game? gameEntity) {
        if (gameEntity == null || string.IsNullOrWhiteSpace(gameEntity.ConfigurationJson)) return _defaultConfig;
        try {
            return JsonSerializer.Deserialize<FruitBlastConfig>(gameEntity.ConfigurationJson) ?? _defaultConfig;
        } catch { return _defaultConfig; }
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
        var round = await ExecuteScopedAsync(async (repo, questService, levelService) => {
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

            var directive = await BrainService.GetNextDirectiveAsync(session.UserId, _gameId, bet.Amount, repo);
            var playerProfile = repo.GetPlayerProfile(session.UserId);
            
            // Get the specific Juice Reservoir jackpot for this game
            var jackpots = repo.GetJackpots().ToList();
            var juiceReservoir = jackpots.FirstOrDefault(j => j.GameId == _gameId && j.Tier == JackpotTier.Special)
                                 ?? repo.GetOrCreateLocalJackpot(_gameId);
            
            if (juiceReservoir.Name != "Juice Reservoir") {
                juiceReservoir.Name = "Juice Reservoir";
                juiceReservoir.Tier = JackpotTier.Special;
                repo.UpdateJackpot(juiceReservoir);
            }

            var gameEntity = repo.GetGame(_gameId);
            var config = LoadConfig(gameEntity);

            var state = new FruitBlastState(config.Rows, config.Cols) { 
                Denomination = denom, 
                JuicePotValue = juiceReservoir.CurrentValue,
                LifetimeExplosions = playerProfile?.FruitBlastLifetimeExplosions ?? 0
            };
            
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            int baseNonce = roundNum * 1000;

            var user = repo.GetUser(session.UserId);
            bool isSimUser = user?.Username?.StartsWith("Sim_") ?? false;
            bool isExcludedFromJackpot = isSimUser || user?.Role == Role.Admin;

            FillGrid(state.Grid, session, baseNonce, directive, config);

            int avalancheCount = 0;
            bool continueAvalanche = true;
            bool meltdownAwarded = false;

            while (continueAvalanche && avalancheCount < 20) {
                var step = new AvalancheStep {
                    GridBefore = CopyGrid(state.Grid, config),
                    JuiceMeterValue = state.JuiceMeter
                };

                // --- Level 3: Vitamin Overload Check ---
                if (state.LifetimeExplosions >= 5000 && !state.IsMegaFruitTriggered) {
                    state.IsMegaFruitTriggered = true;
                    step.MegaFruit = new MegaFruit { TopLeft = new Point { R = 1, C = 1 }, SymbolId = 12 }; // Golden Apple Mega
                    // Transform 3x3 area
                    for(int r=1; r<=3; r++) for(int c=1; c<=3; c++) state.Grid[r][c] = 12;
                    // Reset lifetime counter
                    if (playerProfile != null) playerProfile.FruitBlastLifetimeExplosions = 0;
                }

                var clusters = FindClusters(state.Grid, config);
                if (clusters.Any()) {
                    step.WinningClusters = clusters.SelectMany(c => c).ToList();
                    decimal stepWin = CalculateClusterWin(state.Grid, clusters, bet.Amount, state.JuiceMeter);
                    state.CurrentRoundWin += stepWin;
                    step.WinAmount = stepWin;
                    
                    int explodedCount = step.WinningClusters.Count;
                    state.JuiceMeter += explodedCount;
                    state.LifetimeExplosions += explodedCount;
                    if (playerProfile != null) playerProfile.FruitBlastLifetimeExplosions += explodedCount;

                    // Update Juice Reservoir Contribution (0.1% per exploded fruit) - EXCLUDE SIMS & ADMINS
                    if (!isExcludedFromJackpot) {
                        decimal contribution = bet.Amount * 0.001m * explodedCount;
                        juiceReservoir.CurrentValue += contribution;
                        state.JuicePotValue = juiceReservoir.CurrentValue;
                    }

                    step.AffectedColumns.AddRange(step.WinningClusters.Select(p => p.C).Distinct());
                }

                var explosions = ProcessBombs(state.Grid, config);
                if (explosions.Any()) {
                    step.Explosions = explosions;
                    foreach (var exp in explosions) {
                        foreach (var p in exp.Affected) state.Grid[p.R][p.C] = 0;
                        step.AffectedColumns.AddRange(exp.Affected.Select(p => p.C).Distinct());
                        
                        int affectedCount = exp.Affected.Count;
                        state.JuiceMeter += affectedCount;
                        state.LifetimeExplosions += affectedCount;
                        if (playerProfile != null) playerProfile.FruitBlastLifetimeExplosions += affectedCount;

                        if (!isExcludedFromJackpot) {
                            decimal contribution = bet.Amount * 0.001m * affectedCount;
                            juiceReservoir.CurrentValue += contribution;
                            state.JuicePotValue = juiceReservoir.CurrentValue;
                        }

                        if (exp.Type == 10) {
                             state.TotalMultiplier += 1.0m; 
                             if (state.TotalMultiplier > 50) state.TotalMultiplier = 50; 
                        }
                    }
                }

                // --- Level 1 & 2: Juice Reservoir & Fruit Meltdown Trigger ---
                if (state.JuiceMeter >= 200 && !meltdownAwarded) {
                    state.IsMeltdownTriggered = true;
                    step.MeltdownActive = true;
                    meltdownAwarded = true;
                    
                    // Add Juice Reservoir to win - EXCLUDE SIMS FROM DRAINING POT
                    if (!isExcludedFromJackpot) {
                        state.CurrentRoundWin += juiceReservoir.CurrentValue;
                        // Reset Juice Reservoir to seed value (1000 as per user request)
                        juiceReservoir.CurrentValue = 1000.0m;
                    }
                    
                    // Trigger Meltdown transformation
                    for(int r=0; r<config.Rows; r++) for(int c=0; c<config.Cols; c++) state.Grid[r][c] = 12; 
                    
                    state.JuicePotValue = juiceReservoir.CurrentValue;
                }

                step.AffectedColumns = step.AffectedColumns.Distinct().OrderBy(c => c).ToList();

                if (!clusters.Any() && !explosions.Any() && !step.MeltdownActive) {
                    continueAvalanche = false;
                } else {
                    foreach (var cluster in clusters) {
                        foreach (var p in cluster) state.Grid[p.R][p.C] = 0;
                    }

                    ApplyGravity(state.Grid, config);
                    FillMissing(state.Grid, session, baseNonce + 100 + (avalancheCount * 30), directive, config);
                    
                    state.History.Add(step);
                    avalancheCount++;
                }
            }

            state.IsFinished = true;
            decimal totalWin = Math.Round(state.CurrentRoundWin * state.TotalMultiplier, 2);

            // ... (Max win cap logic) ...
            // HARD CAP: Max win 10,000x bet
            decimal maxWinAllowed = bet.Amount * 10000;
            if (totalWin > maxWinAllowed) {
                totalWin = maxWinAllowed;
                state.MaxWinCap = maxWinAllowed;
            }

            // SMART VAULT PROTECTION: Bypassed for simulation users
            if (totalWin > 0 && !isSimUser) {
                bool isRandom = directive.DecisionType == "Random";
                if (!await VaultService.CanAffordWinAsync(session.UserId, _gameId, totalWin, repo, strictShadowCheck: !isRandom).ConfigureAwait(false)) {
                    var game = repo.GetGame(_gameId);
                    decimal poolAffordable = (game?.PoolBalance ?? 0) * 0.9m; 
                    decimal fallback = Math.Max(bet.Amount, poolAffordable);
                    if (totalWin > fallback) totalWin = Math.Round(fallback, 2);
                }
            }

            var roundId = Guid.NewGuid();

            if (totalWin > 0) {
                repo.UpdateGamePoolBalance(_gameId, -totalWin);
                await VaultService.ProcessWinAsync(session.UserId, totalWin, repo, roundId).ConfigureAwait(false);
                await questService.UpdateProgressAsync(session.UserId, "WinAmount", totalWin, repo, RealTimeService, VaultService).ConfigureAwait(false);
                session.TotalWon += totalWin;
            }

            // Jackpot Trigger Check (for Global and potentially other game jackpots)
            if (!isExcludedFromJackpot) {
                var jackpotResult = await JackpotService.CheckJackpotTrigger(session.GameId, session.ServerSeed, session.ClientSeed, roundNum, repo).ConfigureAwait(false);
                if (jackpotResult.Triggered) {
                    totalWin += jackpotResult.WinAmount;
                }
            }

            if (playerProfile != null) repo.UpdatePlayerProfile(playerProfile);
            repo.UpdateJackpot(juiceReservoir);
            await BrainService.UpdateProfileAsync(session.UserId, bet.Amount, totalWin, repo);

            var shadowDirective = await BrainService.DecideOutcomeAsync(session.UserId, _gameId, bet.Amount, repo, isShadowMode: true);

            var round = new GameRound {
                Id = roundId,
                GameSessionId = sessionId,
                RoundNumber = roundNum,
                TotalBetAmount = bet.Amount,
                TotalWinAmount = totalWin,
                DecisionType = directive.DecisionType,
                TargetWinAmount = directive.TargetWinAmount,
                ShadowBrainResult = JsonSerializer.Serialize(shadowDirective),
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

        // Sync Cache AFTER Transaction Commit
        using (var scope = ScopeFactory.CreateScope()) {
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            var session = repo.GetSession(sessionId);
            if (session != null) await BrainService.SyncProfileToCacheAsync(session.UserId, repo).ConfigureAwait(false);
        }

        return round;
    }

    private void FillGrid(int[][] grid, GameSession session, int nonce, BrainDirective directive, FruitBlastConfig config) {
        for (int r = 0; r < config.Rows; r++) {
            for (int c = 0; c < config.Cols; c++) {
                grid[r][c] = GetWeightedSymbol(session.ServerSeed, session.ClientSeed, nonce + (r * config.Cols + c), directive, config);
            }
        }
        if (directive.DecisionType != "Random" && directive.TargetWinAmount > 0) {
            var clusters = FindClusters(grid, config);
            if (!clusters.Any()) ForceCluster(grid, session, nonce, config);
        }
    }

    private void FillMissing(int[][] grid, GameSession session, int nonce, BrainDirective directive, FruitBlastConfig config) {
        for (int r = 0; r < config.Rows; r++) {
            for (int c = 0; c < config.Cols; c++) {
                if (grid[r][c] == 0) {
                    grid[r][c] = GetWeightedSymbol(session.ServerSeed, session.ClientSeed, nonce + (r * config.Cols + c) + 5000, directive, config);
                }
            }
        }
    }

    private void ForceCluster(int[][] grid, GameSession session, int nonce, FruitBlastConfig config) {
        int symbol = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + 555, 1, 5);
        int startR = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + 666, 1, config.Rows - 1);
        int startC = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + 777, 1, config.Cols - 1);
        for (int r = startR - 1; r <= startR; r++)
            for (int c = startC - 1; c <= startC + 1; c++)
                grid[r][c] = symbol;
    }

    private int GetWeightedSymbol(string serverSeed, string clientSeed, int nonce, BrainDirective directive, FruitBlastConfig config) {
        int val = RngService.GetNextInt(serverSeed, clientSeed, nonce, 1, 1001);
        int boost = (directive.DecisionType == "RetentionHook") ? 20 : 0;

        if (val <= config.FruitThreshold - boost) {
            int fVal = RngService.GetNextInt(serverSeed, clientSeed, nonce + 10000, 1, 101);
            if (fVal <= 25) return 1; // Lemon 25%
            if (fVal <= 50) return 2; // Cherry 25%
            if (fVal <= 70) return 3; // Orange 20%
            if (fVal <= 85) return 4; // Plum 15%
            return 5; // Watermelon 15%
        }
        if (val <= config.AppleThreshold) return 6; // Apple
        if (val <= config.StarThreshold) return 7; // Star
        if (val <= config.TntThreshold + (boost/2)) return 8; // TNT
        if (val <= config.NuclearThreshold + (boost/4)) return 9; // Nuclear
        return 10; // Supernova
    }

    private int[][] CopyGrid(int[][] grid, FruitBlastConfig config) {
        int rows = grid.Length;
        int cols = grid[0].Length;
        var copy = new int[rows][];
        for (int r = 0; r < rows; r++) {
            copy[r] = new int[cols];
            Array.Copy(grid[r], copy[r], cols);
        }
        return copy;
    }

    private List<List<Point>> FindClusters(int[][] grid, FruitBlastConfig config) {
        var clusters = new List<List<Point>>();
        int rows = config.Rows;
        int cols = config.Cols;
        var visited = new bool[rows, cols];
        for (int r = 0; r < rows; r++) {
            for (int c = 0; c < cols; c++) {
                if (!visited[r, c] && (grid[r][c] > 0 && grid[r][c] <= 7 || grid[r][c] == 12)) {
                    var cluster = new List<Point>();
                    DFS(grid, r, c, grid[r][c], visited, cluster, config);
                    if (cluster.Count >= config.MinCluster) clusters.Add(cluster);
                }
            }
        }
        return clusters;
    }

    private void DFS(int[][] grid, int r, int c, int symbol, bool[,] visited, List<Point> cluster, FruitBlastConfig config) {
        if (r < 0 || r >= config.Rows || c < 0 || c >= config.Cols || visited[r, c] || grid[r][c] != symbol) return;
        visited[r, c] = true;
        cluster.Add(new Point { R = r, C = c });
        DFS(grid, r - 1, c, symbol, visited, cluster, config);
        DFS(grid, r + 1, c, symbol, visited, cluster, config);
        DFS(grid, r, c - 1, symbol, visited, cluster, config);
        DFS(grid, r, c + 1, symbol, visited, cluster, config);
    }

    private decimal CalculateClusterWin(int[][] grid, List<List<Point>> clusters, decimal bet, int juiceMeter) {
        decimal totalWin = 0;
        decimal juiceMultiplier = 1.0m;
        if (juiceMeter >= 200) juiceMultiplier = 18.0m; 
        else if (juiceMeter >= 150) juiceMultiplier = 10.0m; 
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

    private void ApplyGravity(int[][] grid, FruitBlastConfig config) {
        for (int c = 0; c < config.Cols; c++) {
            int emptySpot = config.Rows - 1;
            for (int r = config.Rows - 1; r >= 0; r--) {
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

    private List<BombExplosion> ProcessBombs(int[][] grid, FruitBlastConfig config) {
        var explosions = new List<BombExplosion>();
        var bombPositions = new List<Point>();
        for (int r = 0; r < config.Rows; r++) {
            for (int c = 0; c < config.Cols; c++) {
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
                        if (nr >= 0 && nr < config.Rows && nc >= 0 && nc < config.Cols) affected.Add(new Point { R = nr, C = nc });
                    }
            } else if (type == 9) { 
                for (int r = 0; r < config.Rows; r++) affected.Add(new Point { R = r, C = bomb.C });
                for (int c = 0; c < config.Cols; c++) affected.Add(new Point { R = bomb.R, C = c });
            } else if (type == 10) { 
                for (int r = 0; r < config.Rows; r++) for (int c = 0; c < config.Cols; c++) affected.Add(new Point { R = r, C = c });
            }
            explosions.Add(new BombExplosion { Origin = bomb, Affected = affected, Type = type });
        }
        return explosions;
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) => await Task.FromResult(new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId });
    public override async Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) => await Task.CompletedTask;
    public override async Task<object?> GetCurrentState(Guid sessionId) {
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var round = repo.GetLastRound(sessionId);
            var juicePot = repo.GetOrCreateLocalJackpot(_gameId);
            
            if (round == null) {
                var session = await repo.GetSessionAsync(sessionId);
                var gameEntity = repo.GetGame(_gameId);
                var config = LoadConfig(gameEntity);
                var profile = session != null ? repo.GetPlayerProfile(session.UserId) : null;
                
                return new FruitBlastState(config.Rows, config.Cols) { 
                    JuicePotValue = juicePot.CurrentValue,
                    LifetimeExplosions = profile?.FruitBlastLifetimeExplosions ?? 0
                };
            }
            
            var state = JsonSerializer.Deserialize<FruitBlastState>(round.RandomResult);
            if (state != null) {
                state.JuicePotValue = juicePot.CurrentValue; // Ensure UI sees most current value
            }
            return state;
        });
    }
}
