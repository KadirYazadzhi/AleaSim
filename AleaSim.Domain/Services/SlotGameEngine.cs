using System.Collections.Concurrent;
using System.Text.Json;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace AleaSim.Domain.Services;

public class SlotGameEngine : BaseGameEngine {
    private readonly IMemoryCache _cache;
    private readonly ILockService _lockService;

    // Default configuration (Fallback)
    private static readonly SlotGameConfig _defaultConfig = new SlotGameConfig {
        Rows = 4, Cols = 5, PaylinesCount = 20,
        WildSymbol = 8, ScatterSymbol = 10, CollectSymbol = 11, GoldenSymbol = 12,
        BaseStrip = new[] { 
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 5, 
            1, 2, 3, 4, 5, 1, 2, 3, 4, 6, 1, 2, 3, 4, 5, 1, 2, 3, 4, 6, 1, 2, 3, 
            1, 2, 3, 4, 5, 1, 2, 3, 4, 7, 1, 2, 3, 4, 5, 1, 2, 3, 4, 7, 1, 2, 3,
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 5, 1, 2, 3, 4, 8, 1, 2, 3, 4,
            1, 2, 3, 4, 5, 1, 2, 3, 10, 1, 2, 3, 4, 5, 1, 2, 3, 11, 1, 2, 3, 12 
        }, // Ultra-diluted strip (~1% special symbol density) for realistic RTP
        Paylines = new[] {
            new[] {0,0,0,0,0}, new[] {1,1,1,1,1}, new[] {2,2,2,2,2}, new[] {3,3,3,3,3},
            new[] {0,1,2,1,0}, new[] {1,2,3,2,1}, new[] {2,1,0,1,2}, new[] {3,2,1,2,3},
            new[] {0,1,0,1,0}, new[] {1,0,1,0,1}, new[] {2,3,2,3,2}, new[] {3,2,3,2,3},
            new[] {0,0,1,0,0}, new[] {1,1,2,1,1}, new[] {2,2,3,2,2}, new[] {1,1,0,1,1},
            new[] {2,2,1,2,2}, new[] {1,0,0,0,1}, new[] {2,3,3,3,2}, new[] {0,1,1,1,0}
        },
        Paytable = new Dictionary<int, decimal[]> {
            { 1, new[] { 0.1m, 0.2m, 1.0m } }, { 2, new[] { 0.1m, 0.2m, 1.0m } },
            { 3, new[] { 0.2m, 0.5m, 2.0m } }, { 4, new[] { 0.2m, 0.5m, 2.0m } },
            { 5, new[] { 0.5m, 1.0m, 5.0m } }, { 6, new[] { 0.5m, 1.0m, 5.0m } },
            { 7, new[] { 1.0m, 5.0m, 10.0m } }, { 8, new[] { 2.0m, 10.0m, 25.0m } }
        }
    };

    public SlotGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, IMemoryCache cache, ILockService lockService) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {
        _cache = cache;
        _lockService = lockService;
    }

    public enum BellType { Cash, Mini, Minor, Major }
    public class Point { public int R { get; set; } public int C { get; set; } }
    public class BellValue { public Point Pos { get; set; } = new(); public decimal Value { get; set; } public BellType Type { get; set; } }

    public class SlotState {
        public int[][] Grid { get; set; }
        public bool IsRespinActive { get; set; }
        public int RespinLives { get; set; }
        public List<Point> StickyClovers { get; set; } = new();
        public bool HasGoldenClover { get; set; } 
        public bool IsBonusActive { get; set; } 
        public int BonusLives { get; set; }
        public decimal LockedBet { get; set; }
        public decimal Denomination { get; set; } = 0.01m;
        public List<BellValue> BonusBells { get; set; } = new(); 
        public bool WasNudged { get; set; }
        public bool IsGambleActive { get; set; }
        public decimal PendingGambleWin { get; set; }
        
        public SlotState(int rows, int cols) { 
            Grid = new int[rows][];
            for(int r=0; r<rows; r++) Grid[r] = new int[cols]; 
        }
        public SlotState() : this(4, 5) { } // Default constructor for deserialization fallback
    }

    private SlotGameConfig LoadConfig(Game? gameEntity) {
        if (gameEntity == null || string.IsNullOrWhiteSpace(gameEntity.ConfigurationJson)) return _defaultConfig;
        try {
            var config = JsonSerializer.Deserialize<SlotGameConfig>(gameEntity.ConfigurationJson);
            if (config == null) return _defaultConfig;
            
            // Merge defaults if specific sections are missing
            if (config.Paylines == null || config.Paylines.Length == 0) config.Paylines = _defaultConfig.Paylines;
            if (config.Paytable == null || config.Paytable.Count == 0) config.Paytable = _defaultConfig.Paytable;
            if (config.BaseStrip == null || config.BaseStrip.Length == 0) config.BaseStrip = _defaultConfig.BaseStrip;
            
            return config;
        } catch {
            return _defaultConfig;
        }
    }

    public override async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string betData) {
        decimal[] validDenoms = { 0.01m, 0.02m, 0.05m, 0.10m, 0.20m, 0.50m, 1.00m };
        decimal denom = 0.01m; 
        try { 
            var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(betData);
            if (json.TryGetProperty("Denomination", out var d)) denom = d.GetDecimal();
        } catch {}

        decimal dynamicMinBet = Math.Round(10 * denom, 2);
        decimal dynamicMaxBet = Math.Round(100 * denom, 2);
        decimal roundedAmount = Math.Round(amount, 2);

        // Allow 0 amount for respin/bonus triggers handled internally, but PlaceBet usually carries value
        if (amount > 0) {
            if (roundedAmount < dynamicMinBet || roundedAmount > dynamicMaxBet) 
                throw new Exception($"Bet {roundedAmount:C} is invalid. For denomination {denom:C}, min is {dynamicMinBet:C} and max is {dynamicMaxBet:C}.");
            
            if (!validDenoms.Contains(denom)) throw new Exception("Invalid denomination.");
        }

        await base.PlaceBet(userId, sessionId, amount, betData);
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        using var lockHandle = await _lockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));
        
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");

            var gameEntity = repo.GetGame(session.GameId);
            if (gameEntity == null) throw new Exception("Game configuration not found.");
            var config = LoadConfig(gameEntity);

            // 1. Restore State
            string cacheKey = $"slot_state_{sessionId}";
            if (!_cache.TryGetValue(cacheKey, out SlotState? state)) {
                state = string.IsNullOrEmpty(session.GameState) 
                    ? new SlotState(config.Rows, config.Cols) 
                    : JsonSerializer.Deserialize<SlotState>(session.GameState);
            }
            if (state == null) state = new SlotState(config.Rows, config.Cols);
            
            // Ensure Grid matches config size (if config changed mid-session, reset grid)
            if (state.Grid.Length != config.Rows || state.Grid[0].Length != config.Cols) {
                state = new SlotState(config.Rows, config.Cols);
            }

            state.WasNudged = false;
            var lastBet = repo.GetLastBet(sessionId);
            decimal currentBet = lastBet?.Amount ?? 1.0m;
            
            try {
                if (!string.IsNullOrEmpty(lastBet?.BetData)) {
                    var data = JsonSerializer.Deserialize<JsonElement>(lastBet.BetData);
                    if (data.TryGetProperty("Denomination", out var den)) state.Denomination = den.GetDecimal();
                }
            } catch { }

            decimal instantWin = 0;
            bool wasRespinActive = state.IsRespinActive;

            if (state.IsRespinActive || state.IsBonusActive) {
                currentBet = state.LockedBet;
                if (state.IsRespinActive && !state.IsBonusActive) {
                    // Record "fake" bet for respin tracking
                    repo.SaveBet(new Bet { Id = Guid.NewGuid(), GameSessionId = sessionId, UserId = session.UserId, Amount = 0, BetData = "{\"Type\":\"Respin\"}", CreatedAt = DateTime.UtcNow });
                }
            } else {
                state.LockedBet = currentBet;
                state.HasGoldenClover = false; 
            }

            var directive = BrainService.GetNextDirective(session.UserId, session.GameId, currentBet, repo);
            var shadowDirective = BrainService.DecideOutcome(session.UserId, session.GameId, currentBet, repo, isShadowMode: true);
            
            // Dynamic Strip based on Volatility (Simplified: just adds/removes Wilds)
            int[] activeStrip = config.BaseStrip;
            if (directive.VolatilityModifier >= 2.0) {
                // High volatility: Add more Wilds but also more trash
                var list = config.BaseStrip.ToList();
                list.Add(config.WildSymbol); list.Add(config.WildSymbol);
                activeStrip = list.ToArray();
            }

            decimal totalWin = 0;
            int attempts = 0;
            bool success = false;

            do {
                attempts++;
                if (state.IsBonusActive) { 
                    PlayBonusRound(state, session.Seed, repo, config); 
                } 
                else {
                    if (directive.IsNearMiss && attempts == 1) {
                         GenerateNearMissGrid(state, directive.PreferredNearMissSymbol ?? 7, config);
                    } else if (attempts > 40) {
                        // Force Logic to break loop
                        if (directive.TargetWinAmount > 0) ForceWinGrid(state, config);
                        else ForceLoseGrid(state, config);
                    } else {
                        instantWin = PlayStandardRound(state, session.Seed, activeStrip, config);
                    }
                }

                if (state.IsBonusActive) {
                    totalWin = 0; 
                    if (state.BonusLives == 0 || state.BonusBells.Count == config.Rows * config.Cols) {
                        totalWin = state.BonusBells.Sum(b => b.Value);
                        if (state.BonusBells.Count == config.Rows * config.Cols) totalWin *= 1.5m; // 1.5x Multiplier (was 2x)
                        state.IsBonusActive = false; state.IsRespinActive = false; state.StickyClovers.Clear(); state.HasGoldenClover = false;
                    }
                } else {
                    // Fix: Only pay lines on base spin. During Respins, lines are skipped because symbols are sticky.
                    decimal lineWin = wasRespinActive ? 0 : EvaluateGrid(state.Grid, currentBet, config);
                    totalWin = lineWin + instantWin;
                }

                if (state.IsRespinActive || state.IsBonusActive) {
                    success = true; // Always accept partial game states
                    break;
                }

                if (CheckBrainCompliance(directive, totalWin, currentBet)) {
                    success = true;
                    break;
                }
                
                // RNG Cycling (simulate different outcomes)
                session.Seed = RngService.GetNextInt(session.Seed, attempts, 0, int.MaxValue);

            } while (attempts < 50);

            // Fallback if loop failed completely
            if (!success && !state.IsRespinActive && !state.IsBonusActive) {
                ForceLoseGrid(state, config);
                totalWin = 0;
            }

            if (totalWin > 0) {
                if (state.BonusBells.Any(b => b.Type == BellType.Major) && !state.IsBonusActive) {
                    foreach(var b in state.BonusBells.Where(x => x.Type == BellType.Major)) {
                        await JackpotService.ClaimJackpot(JackpotTier.Spades, repo);
                    }
                }

                await VaultService.ProcessWinAsync(session.UserId, totalWin, repo);
                await questService.UpdateProgressAsync(session.UserId, "WinAmount", (int)totalWin, repo, VaultService);
                
                state.IsGambleActive = true;
                state.PendingGambleWin = totalWin;
            } else {
                state.IsGambleActive = false;
                state.PendingGambleWin = 0;
            }
            
            if (!state.IsRespinActive && !state.IsBonusActive) {
                BrainService.UpdateProfile(session.UserId, 0, totalWin, repo);
            }

            _cache.Set(cacheKey, state, TimeSpan.FromMinutes(10));
            session.GameState = JsonSerializer.Serialize(state);

            int roundCount = repo.GetRoundCount(sessionId);

            var round = new GameRound {
                Id = Guid.NewGuid(), GameSessionId = sessionId, TotalBetAmount = currentBet, TotalWinAmount = totalWin,
                RoundNumber = roundCount + 1,
                RandomResult = JsonSerializer.Serialize(new { Grid = state.Grid, state.IsRespinActive, state.IsBonusActive, state.WasNudged, BonusTotal = state.BonusBells.Sum(x=>x.Value), state.Denomination, BonusBells = state.BonusBells }),
                DecisionType = directive.DecisionType, ExecutedAt = DateTime.UtcNow,
                ShadowBrainResult = JsonSerializer.Serialize(shadowDirective)
            };
            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Grid = state.Grid, Win = totalWin, IsRespin = state.IsRespinActive, Nudge = state.WasNudged, Bonus = state.IsBonusActive, Bells = state.BonusBells });
            return round;
        });
    }

    // ---

    private void ForceLoseGrid(SlotState state, SlotGameConfig config) {
        // Fill with a non-winning pattern (e.g. checkerboard of 1 and 2, assuming they don't pay mixed)
        for(int r=0; r<config.Rows; r++) 
            for(int c=0; c<config.Cols; c++) 
                state.Grid[r][c] = ((r+c)%2 == 0) ? 1 : 2;
    }

    private void ForceWinGrid(SlotState state, SlotGameConfig config) {
        // Guaranteed win: First line is Symbol 1 (Lowest pay)
        ForceLoseGrid(state, config); // Clear
        for(int c=0; c<3; c++) state.Grid[0][c] = 1; // 3 of a kind
    }

    private void GenerateNearMissGrid(SlotState state, int symbol, SlotGameConfig config) {
        int nonce = 1000;
        // Random fill first
        for(int r=0; r<config.Rows; r++) for(int c=0; c<config.Cols; c++) state.Grid[r][c] = config.BaseStrip[RngService.GetNextInt(0, nonce++, 0, config.BaseStrip.Length)];
        // Set near miss: 2 symbols on line 1, miss on reel 3
        state.Grid[1][0] = symbol; state.Grid[1][1] = symbol;
        state.Grid[0][2] = symbol; // Miss above
        state.Grid[1][2] = (symbol == 1) ? 2 : 1; // Blocker
    }

    private decimal PlayStandardRound(SlotState state, int seed, int[] strip, SlotGameConfig config) {
        int nonce = 0; decimal coinWin = 0;
        var stickyMap = new HashSet<(int,int)>(state.StickyClovers.Select(p => (p.R, p.C)));
        int[] stops = new int[config.Cols];

        for (int c = 0; c < config.Cols; c++) {
            stops[c] = RngService.GetNextInt(seed, nonce++, 0, strip.Length);
            for (int r = 0; r < config.Rows; r++) {
                if (stickyMap.Contains((r,c))) { state.Grid[r][c] = config.WildSymbol; continue; }
                state.Grid[r][c] = strip[(stops[c] + r) % strip.Length];
            }
        }

        int newClovers = 0;
        for (int r = 0; r < config.Rows; r++) {
            for (int c = 0; c < config.Cols; c++) {
                int sym = state.Grid[r][c];
                if ((sym == config.WildSymbol || sym == config.GoldenSymbol) && !stickyMap.Contains((r,c))) {
                    state.StickyClovers.Add(new Point { R = r, C = c });
                    if (sym == config.GoldenSymbol) state.HasGoldenClover = true;
                    state.Grid[r][c] = config.WildSymbol; newClovers++;
                }
                if (sym == config.CollectSymbol && state.IsRespinActive) coinWin += state.LockedBet;
            }
        }

        if (newClovers > 0) {
            // Requirement: 4+ symbols to START respins, 1+ to continue
            if (state.IsRespinActive || state.StickyClovers.Count >= 4) {
                state.IsRespinActive = true; 
                state.RespinLives = 1; 
            }
        }
        else if (state.IsRespinActive) {
            state.RespinLives--;
        }

        // Mystery Nudge Logic (Simplified)
        if (state.StickyClovers.Count == 4 && !state.IsBonusActive && !state.WasNudged) {
             // 20% Chance
             if (RngService.GetNextDouble(seed, 777) < 0.20) {
                 for (int c = 0; c < config.Cols; c++) {
                     if (state.StickyClovers.Any(p => p.C == c)) continue;
                     state.WasNudged = true;
                     state.RespinLives = 1; // Corrected to 1
                     state.IsRespinActive = true;
                     // Find valid spot
                     for(int r=0; r<config.Rows; r++) {
                         if(state.Grid[r][c] != config.WildSymbol) {
                             state.Grid[r][c] = config.WildSymbol;
                             state.StickyClovers.Add(new Point { R = r, C = c });
                             break;
                         }
                     }
                     break; 
                }
            }
        }

        if (state.IsRespinActive && state.RespinLives <= 0) {
             if (state.StickyClovers.Count >= 6) { // 6+ symbols to trigger Bonus (was 5)
                 state.IsBonusActive = true; 
                 state.BonusLives = 3; 
                 state.IsRespinActive = false; 
                 InitializeBonusGrid(state, seed, config); 
             }
             else { state.IsRespinActive = false; state.StickyClovers.Clear(); }
        }
        return coinWin;
    }

    private void InitializeBonusGrid(SlotState state, int seed, SlotGameConfig config) {
        state.BonusBells.Clear();
        int nonce = 5000;
        for(int r=0; r<config.Rows; r++) for(int c=0; c<config.Cols; c++) state.Grid[r][c] = 0;
        foreach(var p in state.StickyClovers) {
            state.Grid[p.R][p.C] = config.ScatterSymbol;
            decimal val = state.LockedBet * (state.HasGoldenClover ? RngService.GetNextInt(seed, nonce++, 2, 8) : RngService.GetNextInt(seed, nonce++, 1, 5));
            state.BonusBells.Add(new BellValue { Pos = p, Value = val, Type = BellType.Cash });
        }
    }

    private void PlayBonusRound(SlotState state, int seed, IGameRepository repo, SlotGameConfig config) {
        bool landed = false; int nonce = 0;
        for (int r = 0; r < config.Rows; r++) {
            for (int c = 0; c < config.Cols; c++) {
                if (state.Grid[r][c] == config.ScatterSymbol) continue;
                if (RngService.GetNextDouble(seed, nonce++) < 0.01) { // 1% chance per cell (was 1.5%)
                    state.Grid[r][c] = config.ScatterSymbol; landed = true;
                    double tr = RngService.GetNextDouble(seed, nonce++);
                    var bell = new BellValue { Pos = new Point { R=r, C=c } };
                    int minis = state.BonusBells.Count(b => b.Type == BellType.Mini);
                    int minors = state.BonusBells.Count(b => b.Type == BellType.Minor);
                    
                    if (tr < 0.0001) { // 1 in 10,000 (was 1 in 1,000)
                        bell.Type = BellType.Major; 
                        bell.Value = JackpotService.GetTierValue(JackpotTier.Spades, repo);
                    }
                    else if (tr < 0.011 && minors < 3) { 
                        bell.Type = BellType.Minor; 
                        bell.Value = state.LockedBet * 50m;
                    }
                    else if (tr < 0.06 && minis < 5) { 
                        bell.Type = BellType.Mini; 
                        bell.Value = state.LockedBet * 20m;
                    }
                    else { 
                        bell.Type = BellType.Cash; 
                        bell.Value = state.LockedBet * (state.HasGoldenClover ? RngService.GetNextInt(seed, nonce++, 5, 10) : RngService.GetNextInt(seed, nonce++, 1, 10)); 
                    }
                    state.BonusBells.Add(bell);
                }
            }
        }
        if (landed) state.BonusLives = 3; else state.BonusLives--;
    }

    private decimal EvaluateGrid(int[][] grid, decimal bet, SlotGameConfig config) {
        decimal win = 0;
        foreach (var line in config.Paylines) {
            if (line.Length != config.Cols) continue; // Safety check
            int match = grid[line[0]][0]; int count = 1;
            if (match == config.WildSymbol || match == config.GoldenSymbol) match = -1;
            
            for (int c = 1; c < config.Cols; c++) {
                if (IsMatch(match, grid[line[c]][c], out int res, config)) { 
                    count++; 
                    if (match == -1 && res != config.WildSymbol && res != config.GoldenSymbol) match = res; 
                }
                else break;
            }
            if (count >= 3) { 
                int sym = (match == -1) ? config.WildSymbol : match; 
                if (config.Paytable.ContainsKey(sym)) {
                     // Check array bounds
                     int index = count - 3;
                     if (index >= 0 && index < config.Paytable[sym].Length)
                        win += bet * config.Paytable[sym][index]; 
                }
            }
        }
        return win;
    }

    private bool IsMatch(int t, int c, out int r, SlotGameConfig config) {
        r = t; if (t == -1) { r = c; return true; }
        if (t == c || c == config.WildSymbol || c == config.GoldenSymbol) return true;
        // Check "Seven" and "Wild Seven" logic if applicable, otherwise strict match
        // Assuming Wild logic is generic now:
        return false;
    }

    private bool CheckBrainCompliance(BrainDirective d, decimal w, decimal b) {
        if (d.DecisionType == "Random") return true;
        if (d.TargetWinAmount == 0) return w == 0;
        return w >= d.TargetWinAmount * 0.8m && w <= d.TargetWinAmount * 1.2m; // Added upper bound
    }

    public override async Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) {
        using var lockHandle = await _lockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));
        await ExecuteScopedAsync(async (repo, questService, levelService) => {
             var session = repo.GetSession(sessionId);
             string cacheKey = $"slot_state_{sessionId}";
             if (!_cache.TryGetValue(cacheKey, out SlotState? state)) {
                 state = string.IsNullOrEmpty(session.GameState) ? null : JsonSerializer.Deserialize<SlotState>(session.GameState);
             }
             if (state == null || !state.IsGambleActive || state.PendingGambleWin <= 0) throw new Exception("Gamble not available.");

             if (action.ToLower() == "collect") {
                 state.IsGambleActive = false;
                 state.PendingGambleWin = 0;
             }
             else if (action.ToLower() == "gamble") {
                 string choice = actionData.ToLower(); 
                 if (choice != "red" && choice != "black") throw new Exception("Invalid gamble choice.");

                 if (!await VaultService.ProcessBetAsync(userId, state.PendingGambleWin, repo)) {
                     throw new Exception("Insufficient balance to gamble.");
                 }

                 // Use Crypto RNG
                 bool win = RngService.GetNextDouble(0, 0) > 0.5;
                 
                 if (state.PendingGambleWin > 1000) win = false; 

                 if (win) {
                     decimal newWin = state.PendingGambleWin * 2;
                     state.PendingGambleWin = newWin;
                     await VaultService.ProcessWinAsync(userId, newWin, repo);
                     await questService.UpdateProgressAsync(userId, "WinAmount", (int)newWin, repo, VaultService);
                 } else {
                     state.PendingGambleWin = 0;
                     state.IsGambleActive = false; 
                 }
             }

             _cache.Set(cacheKey, state, TimeSpan.FromMinutes(10));
             session.GameState = JsonSerializer.Serialize(state);
             repo.SaveChanges();
             
             await RealTimeService.NotifyGameUpdate(userId, new { GambleResult = state.PendingGambleWin, IsGambleActive = state.IsGambleActive });
        });
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) => new Outcome { GameRoundId = roundId };
    
    // Implemented!
    public override async Task<object?> GetCurrentState(Guid sessionId) {
        if (_cache.TryGetValue($"slot_state_{sessionId}", out SlotState? state)) {
            return state;
        }
        // Fallback to DB
        return await ExecuteScopedAsync(async (repo, _, _) => {
            var session = repo.GetSession(sessionId);
            if (session == null || string.IsNullOrEmpty(session.GameState)) return null;
            return JsonSerializer.Deserialize<SlotState>(session.GameState);
        });
    }
}
