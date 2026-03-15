using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;

namespace AleaSim.Domain.Services;

public class SlotGameEngine : BaseGameEngine {
    private readonly IRedisCacheService _cache;

    private static readonly SlotGameConfig _defaultConfig = new SlotGameConfig {
        Rows = 4, Cols = 5, PaylinesCount = 20,
        WildSymbol = 8, ScatterSymbol = 9, CollectSymbol = 11, GoldenSymbol = 12,
        BaseStrip = new[] { 
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 11, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 40
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 8,  1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 80 (Clover)
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 11, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 120
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 9,  1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 160 (Scatter)
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 11, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 200
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 8,  1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 240 (Clover)
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 11, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 280
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 12, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 320 (Golden)
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 11, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, // 360
            1, 2, 3, 4, 5, 1, 2, 3, 4, 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 8,  1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6, 7, 1, 2, 3, 4, 5, 6  // 400 (Clover)
        },
        Paylines = new[] {
            new[] {0,0,0,0,0}, new[] {1,1,1,1,1}, new[] {2,2,2,2,2}, new[] {3,3,3,3,3},
            new[] {0,1,2,1,0}, new[] {1,2,3,2,1}, new[] {2,1,0,1,2}, new[] {3,2,1,2,3},
            new[] {0,1,0,1,0}, new[] {1,0,1,0,1}, new[] {2,3,2,3,2}, new[] {3,2,3,2,3},
            new[] {0,0,1,0,0}, new[] {1,1,2,1,1}, new[] {2,2,3,2,2}, new[] {1,1,0,1,1},
            new[] {2,2,1,2,2}, new[] {1,0,0,0,1}, new[] {2,3,3,3,2}, new[] {0,1,1,1,0}
        },
        Paytable = new Dictionary<int, decimal[]> {
            { 1, new[] { 4.0m, 15.0m, 45.0m } }, { 2, new[] { 4.0m, 15.0m, 45.0m } },
            { 3, new[] { 8.0m, 20.0m, 65.0m } }, { 4, new[] { 8.0m, 20.0m, 65.0m } },
            { 5, new[] { 12.0m, 38.0m, 110.0m } }, { 6, new[] { 12.0m, 38.0m, 110.0m } },
            { 7, new[] { 25.0m, 100.0m, 300.0m } }, { 8, new[] { 25.0m, 120.0m, 400.0m } }, 
            { 9, new[] { 18.0m, 60.0m, 200.0m } }, { 12, new[] { 60.0m, 300.0m, 1000.0m } }
        }
    };

    public SlotGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, IRedisCacheService cache, ILockService lockService)
        : base(rng, vault, brain, promo, jackpot, realTime, scope, lockService) {
        _cache = cache;
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
        public SlotState() : this(4, 5) { }
    }

    private SlotGameConfig LoadConfig(Game? gameEntity) {
        if (gameEntity == null || string.IsNullOrWhiteSpace(gameEntity.ConfigurationJson)) return _defaultConfig;
        try {
            var config = JsonSerializer.Deserialize<SlotGameConfig>(gameEntity.ConfigurationJson);
            if (config == null) return _defaultConfig;
            if (config.Paylines == null || config.Paylines.Length == 0) config.Paylines = _defaultConfig.Paylines;
            if (config.Paytable == null || config.Paytable.Count == 0) config.Paytable = _defaultConfig.Paytable;
            if (config.BaseStrip == null || config.BaseStrip.Length == 0) config.BaseStrip = _defaultConfig.BaseStrip;
            return config;
        } catch { return _defaultConfig; }
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
        if (amount > 0) {
            decimal min = 10 * denom;
            if (amount < min) throw new Exception($"Min bet is {min:C2}");
        }
        await base.PlaceBet(userId, sessionId, amount, betData);
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        using var lockHandle = await LockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));
        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");
            var gameEntity = repo.GetGame(session.GameId);
            var config = LoadConfig(gameEntity);

            string cacheKey = $"slot_state_{sessionId}";
            var state = await _cache.GetAsync<SlotState>(cacheKey);
            if (state == null) state = string.IsNullOrEmpty(session.GameState) ? new SlotState(config.Rows, config.Cols) : JsonSerializer.Deserialize<SlotState>(session.GameState);
            if (state == null) state = new SlotState(config.Rows, config.Cols);

            state.WasNudged = false;
            int roundNum = repo.GetRoundCount(sessionId) + 1;
            var lastBet = repo.GetLastBet(sessionId);
            decimal currentBet = lastBet?.Amount ?? 1.0m;

            if (state.IsRespinActive || state.IsBonusActive) {
                currentBet = state.LockedBet;
            } else {
                state.LockedBet = currentBet;
                state.HasGoldenClover = false; 
                state.StickyClovers.Clear(); 
            }

            var directive = BrainService.GetNextDirective(session.UserId, session.GameId, currentBet, repo);
            var shadowDirective = BrainService.DecideOutcome(session.UserId, session.GameId, currentBet, repo, isShadowMode: true);
            
            decimal totalWin = 0;
            int attempts = 0;
            bool success = false;

            do {
                attempts++;
                int attemptOffset = (roundNum * 10000) + (attempts * 1000);
                decimal instantWin = 0;

                if (state.IsBonusActive) { 
                    PlayBonusRound(state, session.ServerSeed, session.ClientSeed, attemptOffset, repo, config); 
                } else {
                    if (directive.DecisionType != "Random" && attempts > 30) {
                        ForceWinGrid(state, config, directive.TargetWinAmount, currentBet);
                    } else {
                        instantWin = PlayStandardRound(state, session.ServerSeed, session.ClientSeed, attemptOffset, config.BaseStrip, config);
                    }
                }

                if (state.IsBonusActive) {
                    totalWin = 0; 
                    if (state.BonusLives == 0 || state.BonusBells.Count == config.Rows * config.Cols) {
                        totalWin = state.BonusBells.Sum(b => b.Value);
                        if (state.BonusBells.Count == config.Rows * config.Cols) totalWin *= 1.5m;
                        state.IsBonusActive = false; state.IsRespinActive = false; state.StickyClovers.Clear();
                    }
                } else {
                    decimal lineWin = EvaluateGrid(state.Grid, currentBet, config);
                    totalWin = lineWin + instantWin;
                }

                if (state.IsRespinActive || state.IsBonusActive || CheckBrainCompliance(directive, totalWin, currentBet)) {
                    success = true; break;
                }
            } while (attempts < 50);

            if (totalWin > 0) {
                repo.UpdateGamePoolBalance(session.GameId, -totalWin);
                await VaultService.ProcessWinAsync(session.UserId, totalWin, repo);
                await questService.UpdateProgressAsync(session.UserId, "WinAmount", totalWin, repo, RealTimeService, VaultService);
            }
            
            if (!state.IsRespinActive && !state.IsBonusActive) BrainService.UpdateProfile(session.UserId, 0, totalWin, repo);

            await _cache.SetAsync(cacheKey, state, TimeSpan.FromMinutes(10));
            session.GameState = JsonSerializer.Serialize(state);
            repo.SaveRound(new GameRound {
                Id = Guid.NewGuid(), GameSessionId = sessionId, TotalBetAmount = currentBet, TotalWinAmount = totalWin,
                RoundNumber = roundNum, DecisionType = directive.DecisionType, ExecutedAt = DateTime.UtcNow,
                RandomResult = JsonSerializer.Serialize(new { Grid = state.Grid, state.IsRespinActive, state.IsBonusActive, state.WasNudged, state.BonusLives, BonusTotal = state.BonusBells.Sum(x=>x.Value), state.Denomination, BonusBells = state.BonusBells, WinningLines = GetWinningLines(state.Grid, state.LockedBet, config) }),
                ServerSeed = session.ServerSeed, ClientSeed = session.ClientSeed, Nonce = roundNum
            });
            repo.UpdateSession(session);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Grid = state.Grid, Win = totalWin, IsRespin = state.IsRespinActive, Bonus = state.IsBonusActive });
            return repo.GetLastRound(sessionId)!;
        });
    }

    private void ForceWinGrid(SlotState state, SlotGameConfig config, decimal targetWin, decimal currentBet) {
        for(int r=0; r<config.Rows; r++) for(int c=0; c<config.Cols; c++) state.Grid[r][c] = (r+c)%2==0?1:2;
        decimal lineBet = currentBet / config.PaylinesCount;
        int sym = targetWin > currentBet * 5 ? 7 : 3;
        int count = targetWin > currentBet * 10 ? 5 : 3;
        for(int r=0; r<2; r++) for(int c=0; c<count; c++) state.Grid[r][c] = sym; // Hit multiple lines
    }

    private decimal PlayStandardRound(SlotState state, string ss, string cs, int off, int[] strip, SlotGameConfig cfg) {
        decimal coins = 0; int n = off;
        // 1. Generate random symbols for all cells
        for (int c = 0; c < cfg.Cols; c++) {
            int stop = RngService.GetNextInt(ss, cs, n++, 0, strip.Length);
            for (int r = 0; r < cfg.Rows; r++) state.Grid[r][c] = strip[(stop + r) % strip.Length];
        }

        // 2. Force Sticky Clovers back onto the grid (so they act as Wilds for Paylines)
        foreach (var p in state.StickyClovers) {
            if (p.R < cfg.Rows && p.C < cfg.Cols) {
                state.Grid[p.R][p.C] = 8; // Sticky Wild
            }
        }

        int newClovers = 0;
        // 3. Scan for NEW clovers that landed in this spin
        for (int r = 0; r < cfg.Rows; r++) {
            for (int c = 0; c < cfg.Cols; c++) {
                int sym = state.Grid[r][c];
                if (sym == 8 || sym == 12) {
                    // Only add if NOT already in the sticky list
                    if (!state.StickyClovers.Any(p => p.R == r && p.C == c)) {
                        state.StickyClovers.Add(new Point { R = r, C = c });
                        newClovers++;
                    }
                }
                // Symbol 11 (Collect) gives instant win during respins
                if (state.Grid[r][c] == 11 && state.IsRespinActive) coins += state.LockedBet * 0.2m; 
            }
        }

        // 4. Update Respin State logic
        if (newClovers > 0) {
            state.IsRespinActive = true; 
            state.RespinLives = 3; // Reset to 3 lives per design doc
        } else if (state.IsRespinActive) {
            state.RespinLives--;
        }

        // 5. Check transition to Bonus Game (Hold & Win)
        if (state.IsRespinActive && state.RespinLives <= 0) {
            if (state.StickyClovers.Count >= 5) { // Design requires 5+ Clovers for Bonus
                state.IsBonusActive = true; 
                state.BonusLives = 3; 
                state.IsRespinActive = false;
                InitializeBonusGrid(state, ss, cs, off, cfg);
            } else { 
                state.IsRespinActive = false; 
                state.StickyClovers.Clear(); 
            }
        }
        return coins;
    }

    private void InitializeBonusGrid(SlotState state, string ss, string cs, int off, SlotGameConfig cfg) {
        state.BonusBells.Clear(); 
        int n = off + 5000;
        
        // Clear grid for bonus mode
        for(int r=0; r<cfg.Rows; r++) for(int c=0; c<cfg.Cols; c++) state.Grid[r][c] = 0;
        
        // Natural scaling: High bets get better bonus floor
        int minMult = state.LockedBet >= 50m ? 4 : 2;
        int maxMult = state.LockedBet >= 50m ? 12 : 8;

        // Convert unique sticky clovers to bells
        foreach(var p in state.StickyClovers) {
            state.Grid[p.R][p.C] = 9; // Bell symbol
            state.BonusBells.Add(new BellValue { 
                Pos = p, 
                Value = state.LockedBet * RngService.GetNextInt(ss, cs, n++, minMult, maxMult), 
                Type = BellType.Cash 
            });
        }
    }

    private void PlayBonusRound(SlotState state, string ss, string cs, int off, IGameRepository repo, SlotGameConfig cfg) {
        bool hit = false; int n = off;
        
        // Natural scaling for bonus hits
        int minMult = state.LockedBet >= 50m ? 5 : 2;
        int maxMult = state.LockedBet >= 50m ? 40 : 30;

        for (int r = 0; r < cfg.Rows; r++) for (int c = 0; c < cfg.Cols; c++) {
            if (state.Grid[r][c] == 9) continue;
            if (RngService.GetNextDouble(ss, cs, n++) < 0.03) { 
                state.Grid[r][c] = 9; hit = true;
                state.BonusBells.Add(new BellValue { 
                    Pos = new Point { R=r, C=c }, 
                    Value = state.LockedBet * RngService.GetNextInt(ss, cs, n++, minMult, maxMult), 
                    Type = BellType.Cash 
                });
            }
        }
        if (hit) state.BonusLives = 3; else state.BonusLives--;
    }

    private decimal EvaluateGrid(int[][] grid, decimal bet, SlotGameConfig cfg) {
        decimal win = 0; decimal lb = bet / cfg.PaylinesCount;
        foreach (var line in cfg.Paylines) {
            int match = grid[line[0]][0]; int count = 1;
            if (match == 8 || match == 12) match = -1;
            for (int c = 1; c < cfg.Cols; c++) {
                int cur = grid[line[c]][c];
                if (match == -1) { if (cur <= 8 || cur == 12) { count++; if (cur != 8 && cur != 12) match = cur; } else break; }
                else if (cur == match || cur == 8 || (cur == 12 && match <= 7)) count++;
                else break;
            }
            if (count >= 3) {
                int s = match == -1 ? 8 : match;
                if (cfg.Paytable.TryGetValue(s, out var m) && count-3 < m.Length) win += lb * m[count-3];
            }
        }
        return win;
    }

    private List<object> GetWinningLines(int[][] grid, decimal bet, SlotGameConfig cfg) {
        var lines = new List<object>(); decimal lb = bet / cfg.PaylinesCount;
        for (int i = 0; i < cfg.Paylines.Length; i++) {
            var line = cfg.Paylines[i]; int match = grid[line[0]][0]; int count = 1;
            if (match == 8 || match == 12) match = -1;
            for (int c = 1; c < cfg.Cols; c++) {
                int cur = grid[line[c]][c];
                if (match == -1) { if (cur <= 8 || cur == 12) { count++; if (cur != 8 && cur != 12) match = cur; } else break; }
                else if (cur == match || cur == 8 || (cur == 12 && match <= 7)) count++;
                else break;
            }
            if (count >= 3) {
                int s = match == -1 ? 8 : match;
                if (cfg.Paytable.TryGetValue(s, out var m) && count-3 < m.Length)
                    lines.Add(new { LineIndex = i, SymbolId = s, Count = count, Payout = lb * m[count-3] });
            }
        }
        return lines;
    }

    private bool CheckBrainCompliance(BrainDirective d, decimal w, decimal b) {
        if (d.DecisionType == "Random") return true;
        if (d.TargetWinAmount <= 0) return w == 0;
        return w >= d.TargetWinAmount * 0.5m; 
    }

    public override async Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) {
        using var lockHandle = await LockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));
        await ExecuteScopedAsync(async (repo, questService, levelService) => {
             var session = repo.GetSession(sessionId);
             if (session == null) throw new Exception("Session not found.");
             var state = await _cache.GetAsync<SlotState>($"slot_state_{sessionId}");
             if (state == null) throw new Exception("Gamble not available.");
             if (action.ToLower() == "collect") { state.IsGambleActive = false; state.PendingGambleWin = 0; }
             else if (action.ToLower() == "gamble") {
                 if (!state.IsGambleActive || state.PendingGambleWin <= 0) throw new Exception("Gamble not available.");
                 if (!await VaultService.ProcessBetAsync(userId, state.PendingGambleWin, repo)) throw new Exception("Insufficient balance.");
                 if (RngService.GetNextDouble(0, 0) > 0.5) {
                     decimal nw = state.PendingGambleWin * 2; state.PendingGambleWin = nw;
                     repo.UpdateGamePoolBalance(session.GameId, -nw);
                     await VaultService.ProcessWinAsync(userId, nw, repo);
                 } else { state.PendingGambleWin = 0; state.IsGambleActive = false; }
             }
             await _cache.SetAsync($"slot_state_{sessionId}", state, TimeSpan.FromMinutes(10));
             session.GameState = JsonSerializer.Serialize(state);
             repo.SaveChanges();
        });
    }

    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome { GameRoundId = roundId });
    public override async Task<object?> GetCurrentState(Guid sessionId) {
        var s = await _cache.GetAsync<SlotState>($"slot_state_{sessionId}");
        if (s != null) return s;
        return await ExecuteScopedAsync((repo, _, _) => {
            var ss = repo.GetSession(sessionId);
            return Task.FromResult<object?>(ss == null || string.IsNullOrEmpty(ss.GameState) ? null : JsonSerializer.Deserialize<SlotState>(ss.GameState));
        });
    }
}
