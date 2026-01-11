using System.Collections.Concurrent;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using AleaSim.Domain.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AleaSim.Domain.Services;

public class SlotGameEngine : BaseGameEngine {
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _sessionLocks = new();
    private const int Rows = 4;
    private const int Cols = 5;
    private const int PaylinesCount = 20;
    private readonly IMemoryCache _cache;
    
    private const int SYM_WILD_CLOVER = 8;
    private const int SYM_WILD_SEVEN = 7;
    private const int SYM_BELL = 10;
    private const int SYM_COLLECT_COIN = 11;
    private const int SYM_GOLDEN_CLOVER = 12;
    
    private readonly int[] _baseStrip = { 1, 2, 3, 4, 5, 1, 2, 3, 4, 6, 1, 2, 3, 5, 1, 2, 7, 1, 3, 4, 8, 1, 2, 3, 4, 5, 1, 2, 8, 3, 4, 11, 12 };

    private readonly int[][] _paylines = new int[][] {
        new[] {0,0,0,0,0}, new[] {1,1,1,1,1}, new[] {2,2,2,2,2}, new[] {3,3,3,3,3},
        new[] {0,1,2,1,0}, new[] {1,2,3,2,1}, new[] {2,1,0,1,2}, new[] {3,2,1,2,3},
        new[] {0,1,0,1,0}, new[] {1,0,1,0,1}, new[] {2,3,2,3,2}, new[] {3,2,3,2,3},
        new[] {0,0,1,0,0}, new[] {1,1,2,1,1}, new[] {2,2,3,2,2}, new[] {1,1,0,1,1},
        new[] {2,2,1,2,2}, new[] {1,0,0,0,1}, new[] {2,3,3,3,2}, new[] {0,1,1,1,0}
    };

    private readonly Dictionary<int, decimal[]> _paytable = new() {
        { 1, new[] { 0.2m, 0.5m, 2.0m } }, { 2, new[] { 0.2m, 0.5m, 2.0m } },
        { 3, new[] { 0.4m, 1.0m, 4.0m } }, { 4, new[] { 0.5m, 1.5m, 5.0m } },
        { 5, new[] { 1.0m, 3.0m, 10.0m } }, { 6, new[] { 1.5m, 5.0m, 15.0m } },
        { 7, new[] { 2.5m, 10.0m, 25.0m } }, { 8, new[] { 5.0m, 20.0m, 50.0m } }
    };

    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public SlotGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, IMemoryCache cache) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {   
        _cache = cache;
    }

    public enum BellType { Cash, Mini, Minor, Major }
    public class Point { public int R { get; set; } public int C { get; set; } }
    public class BellValue { public Point Pos { get; set; } = new(); public decimal Value { get; set; } public BellType Type { get; set; } }

    public class SlotState {
        public int[][] Grid { get; set; } = new int[Rows][];
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
        // Gamble Fields
        public bool IsGambleActive { get; set; }
        public decimal PendingGambleWin { get; set; }
        
        public SlotState() { for(int r=0; r<Rows; r++) Grid[r] = new int[Cols]; }
    }

    
    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        var semaphore = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try {
            return await ExecuteScopedAsync(async (repo, questService, levelService) => {
                var session = repo.GetSession(sessionId);
                if (session == null) throw new Exception("Session not found");

                string cacheKey = $"slot_state_{sessionId}";
                if (!_cache.TryGetValue(cacheKey, out SlotState? state)) {
                    state = string.IsNullOrEmpty(session.GameState) 
                        ? new SlotState() 
                        : JsonSerializer.Deserialize<SlotState>(session.GameState) ?? new SlotState();
                }

                state!.WasNudged = false;
                var lastBet = repo.GetLastBet(sessionId);
                decimal currentBet = lastBet?.Amount ?? 1.0m;
                
                try {
                    if (!string.IsNullOrEmpty(lastBet?.BetData)) {
                        var data = JsonSerializer.Deserialize<JsonElement>(lastBet.BetData);
                        if (data.TryGetProperty("Denomination", out var den)) {
                            state.Denomination = den.GetDecimal();
                        }
                    }
                } catch { }

                decimal instantWin = 0;

                if (state.IsRespinActive || state.IsBonusActive) {
                    currentBet = state.LockedBet;
                    if (state.IsRespinActive && !state.IsBonusActive) {
                        repo.SaveBet(new Bet { Id = Guid.NewGuid(), GameSessionId = sessionId, UserId = session.UserId, Amount = 0, BetData = "{\"Type\":\"Respin\"}", CreatedAt = DateTime.UtcNow });
                    }
                } else {
                    state.LockedBet = currentBet;
                    state.HasGoldenClover = false; 
                }

                var directive = BrainService.GetNextDirective(session.UserId, GameId, currentBet, repo);
                
                int[] activeStrip = _baseStrip;
                if (directive.VolatilityModifier >= 2.0) {
                    activeStrip = _baseStrip.Select(s => (s == 1 || s == 2) ? (RngService.GetNextDouble(session.Seed, 999) > 0.5 ? s : 7) : s).ToArray();
                }

                decimal totalWin = 0;
                int attempts = 0;

                do {
                    attempts++;
                    if (state.IsBonusActive) { 
                        PlayBonusRound(state, session.Seed + attempts, repo); 
                        session.GameState = JsonSerializer.Serialize(state); 
                        repo.SaveChanges(); 
                    } 
                    else {
                        if (directive.IsNearMiss && attempts == 1) GenerateNearMissGrid(state, directive.PreferredNearMissSymbol ?? 7, session.Seed);
                        else instantWin = PlayStandardRound(state, session.Seed + attempts, activeStrip);
                        
                        session.GameState = JsonSerializer.Serialize(state);
                        repo.SaveChanges();
                    }

                    if (state.IsBonusActive) {
                        totalWin = 0; 
                        if (state.BonusLives == 0 || state.BonusBells.Count == Rows * Cols) {
                            totalWin = state.BonusBells.Sum(b => b.Value);
                            if (state.BonusBells.Count == Rows * Cols) totalWin *= 2; 
                            state.IsBonusActive = false; state.IsRespinActive = false; state.StickyClovers.Clear(); state.HasGoldenClover = false;
                        }
                    } else {
                        totalWin = EvaluateGrid(state.Grid, currentBet) + instantWin;
                    }

                    if (state.IsRespinActive || state.IsBonusActive || CheckBrainCompliance(directive, totalWin, currentBet)) break;
                } while (attempts < 50);

                if (totalWin > 0) {
                    if (state.BonusBells.Any(b => b.Type == BellType.Major) && !state.IsBonusActive) {
                        foreach(var b in state.BonusBells.Where(x => x.Type == BellType.Major)) {
                            JackpotService.ClaimJackpot(JackpotTier.Spades, repo);
                        }
                    }

                    VaultService.ProcessWin(session.UserId, totalWin, repo);
                    questService.UpdateProgress(session.UserId, "WinAmount", (int)totalWin, repo, VaultService);
                    
                    // Enable Gamble
                    state.IsGambleActive = true;
                    state.PendingGambleWin = totalWin;
                } else {
                    state.IsGambleActive = false;
                    state.PendingGambleWin = 0;
                }
                
                if (!state.IsRespinActive && !state.IsBonusActive) {
                    BrainService.UpdateProfile(session.UserId, 0, totalWin);
                }

                _cache.Set(cacheKey, state, TimeSpan.FromMinutes(10));
                session.GameState = JsonSerializer.Serialize(state);

                var round = new GameRound {
                    Id = Guid.NewGuid(), GameSessionId = sessionId, TotalBetAmount = currentBet, TotalWinAmount = totalWin,
                    RandomResult = JsonSerializer.Serialize(new { Grid = state.Grid, state.IsRespinActive, state.IsBonusActive, state.WasNudged, BonusTotal = state.BonusBells.Sum(x=>x.Value), state.Denomination, BonusBells = state.BonusBells }),
                    DecisionType = directive.DecisionType, ExecutedAt = DateTime.UtcNow
                };
                repo.SaveRound(round);
                await RealTimeService.NotifyGameUpdate(session.UserId, new { Grid = state.Grid, Win = totalWin, IsRespin = state.IsRespinActive, Nudge = state.WasNudged, Bonus = state.IsBonusActive, Bells = state.BonusBells });
                return round;
            });
        } finally {
            semaphore.Release();
        }
    }

    private void GenerateNearMissGrid(SlotState state, int symbol, int seed) {
        int nonce = 1000;
        for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) state.Grid[r][c] = _baseStrip[RngService.GetNextInt(seed, nonce++, 0, _baseStrip.Length)];
        state.Grid[1][0] = symbol; state.Grid[1][1] = symbol;
        state.Grid[RngService.GetNextDouble(seed, nonce) > 0.5 ? 0 : 2][2] = symbol;
        state.Grid[1][2] = (symbol == 1) ? 2 : 1; 
    }

    private decimal PlayStandardRound(SlotState state, int seed, int[] strip) {
        int nonce = 0; decimal coinWin = 0;
        var stickyMap = new HashSet<(int,int)>(state.StickyClovers.Select(p => (p.R, p.C)));
        int[] stops = new int[Cols];

        for (int c = 0; c < Cols; c++) {
            stops[c] = RngService.GetNextInt(seed, nonce++, 0, strip.Length);
            for (int r = 0; r < Rows; r++) {
                if (stickyMap.Contains((r,c))) { state.Grid[r][c] = SYM_WILD_CLOVER; continue; }
                state.Grid[r][c] = strip[(stops[c] + r) % strip.Length];
            }
        }

        int newClovers = 0;
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                int sym = state.Grid[r][c];
                if ((sym == SYM_WILD_CLOVER || sym == SYM_GOLDEN_CLOVER) && !stickyMap.Contains((r,c))) {
                    state.StickyClovers.Add(new Point { R = r, C = c });
                    if (sym == SYM_GOLDEN_CLOVER) state.HasGoldenClover = true;
                    state.Grid[r][c] = SYM_WILD_CLOVER; newClovers++;
                }
                if (sym == SYM_COLLECT_COIN && state.IsRespinActive) coinWin += state.LockedBet;
            }
        }

        if (newClovers > 0) { state.IsRespinActive = true; state.RespinLives = 3; }
        else if (state.IsRespinActive) {
            state.RespinLives--;
        }

        // Mystery Nudge: "The Juice"
        // If we have 4 clovers, check if we can nudge a 5th one in
        if (state.StickyClovers.Count == 4 && !state.IsBonusActive && !state.WasNudged) {
             for (int c = 0; c < Cols; c++) {
                 // Skip columns that already have a clover
                 if (state.StickyClovers.Any(p => p.C == c)) continue;

                 // Check neighbors in the strip (simplified check using grid for now as we don't store full strip pos per reel easily)
                 // Actually, we need the stop position to do a true strip nudge.
                 // Since we don't persist 'stops' in State, we'll simulate it:
                 // 20% chance to "find" a clover nearby if one is missing
                 if (RngService.GetNextDouble(seed, 777) < 0.20) {
                     state.WasNudged = true;
                     state.RespinLives = 3; // Reset lives!
                     state.IsRespinActive = true;
                     
                     // Force the visual grid update
                     // Find an empty row in this col
                     for(int r=0; r<Rows; r++) {
                         if(state.Grid[r][c] != SYM_WILD_CLOVER) {
                             state.Grid[r][c] = SYM_WILD_CLOVER;
                             state.StickyClovers.Add(new Point { R = r, C = c });
                             break;
                         }
                     }
                     break; // Only one nudge
                 }
            }
        }

        if (state.IsRespinActive && state.RespinLives <= 0) {
             if (state.StickyClovers.Count >= 5) { state.IsBonusActive = true; state.BonusLives = 3; state.IsRespinActive = false; InitializeBonusGrid(state, seed); }
             else { state.IsRespinActive = false; state.StickyClovers.Clear(); }
        }
        return coinWin;
    }

    private void InitializeBonusGrid(SlotState state, int seed) {
        state.BonusBells.Clear();
        int nonce = 5000;
        for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) state.Grid[r][c] = 0;
        foreach(var p in state.StickyClovers) {
            state.Grid[p.R][p.C] = SYM_BELL;
            decimal val = state.LockedBet * (state.HasGoldenClover ? RngService.GetNextInt(seed, nonce++, 5, 20) : RngService.GetNextInt(seed, nonce++, 1, 10));
            state.BonusBells.Add(new BellValue { Pos = p, Value = val, Type = BellType.Cash });
        }
    }

    private void PlayBonusRound(SlotState state, int seed, IGameRepository repo) {
        bool landed = false; int nonce = 0;
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                if (state.Grid[r][c] == SYM_BELL) continue;
                if (RngService.GetNextDouble(seed, nonce++) < 0.08) {
                    state.Grid[r][c] = SYM_BELL; landed = true;
                    double tr = RngService.GetNextDouble(seed, nonce++);
                    var bell = new BellValue { Pos = new Point { R=r, C=c } };
                    int minis = state.BonusBells.Count(b => b.Type == BellType.Mini);
                    int minors = state.BonusBells.Count(b => b.Type == BellType.Minor);
                    
                    // FIXED: Mini/Minor are fixed multipliers based on bet. Major is progressive.
                    if (tr < 0.001) { 
                        bell.Type = BellType.Major; // Spades (Progressive)
                        bell.Value = JackpotService.GetTierValue(JackpotTier.Spades, repo);
                    }
                    else if (tr < 0.011 && minors < 3) { 
                        bell.Type = BellType.Minor; // Minor (Fixed 50x)
                        bell.Value = state.LockedBet * 50m;
                    }
                    else if (tr < 0.06 && minis < 5) { 
                        bell.Type = BellType.Mini; // Mini (Fixed 20x)
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

    // ... (EvaluateGrid, IsMatch, CheckBrainCompliance, ProcessAction, GetOutcome, GetCurrentState same as before)
    private decimal EvaluateGrid(int[][] grid, decimal bet) {
        decimal win = 0;
        foreach (var line in _paylines) {
            int match = grid[line[0]][0]; int count = 1;
            if (match == SYM_WILD_CLOVER || match == SYM_GOLDEN_CLOVER) match = -1;
            for (int c = 1; c < Cols; c++) {
                if (IsMatch(match, grid[line[c]][c], out int res)) { count++; if (match == -1 && res != SYM_WILD_CLOVER && res != SYM_GOLDEN_CLOVER) match = res; }
                else break;
            }
            if (count >= 3) { int sym = (match == -1) ? SYM_WILD_CLOVER : match; if (_paytable.ContainsKey(sym)) win += bet * _paytable[sym][count - 3]; }
        }
        return win;
    }

    private bool IsMatch(int t, int c, out int r) {
        r = t; if (t == -1) { r = c; return true; }
        if (t == c || c == SYM_WILD_CLOVER || c == SYM_GOLDEN_CLOVER) return true;
        if ((c == SYM_WILD_SEVEN && t <= 6) || (t == SYM_WILD_SEVEN && c <= 6)) return true;
        return false;
    }

    private bool CheckBrainCompliance(BrainDirective d, decimal w, decimal b) {
        if (d.DecisionType == "Random") return true;
        if (d.TargetWinAmount == 0) return w == 0;
        return w >= d.TargetWinAmount * 0.8m;
    }

    public override async Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) {
        await ExecuteScopedAsync(async (repo, questService, levelService) => {
             var session = repo.GetSession(sessionId);
             string cacheKey = $"slot_state_{sessionId}";
             if (!_cache.TryGetValue(cacheKey, out SlotState? state)) {
                 state = string.IsNullOrEmpty(session.GameState) ? new SlotState() : JsonSerializer.Deserialize<SlotState>(session.GameState);
             }
             if (state == null || !state.IsGambleActive || state.PendingGambleWin <= 0) throw new Exception("Gamble not available.");

             if (action.ToLower() == "collect") {
                 state.IsGambleActive = false;
                 state.PendingGambleWin = 0;
                 // Money is already in balance from the spin win, so just clear state
             }
             else if (action.ToLower() == "gamble") {
                 string choice = actionData.ToLower(); // "red" or "black"
                 if (choice != "red" && choice != "black") throw new Exception("Invalid gamble choice.");

                 // Deduct the amount to put it at risk
                 if (!VaultService.ProcessBet(userId, state.PendingGambleWin, repo)) {
                     throw new Exception("Insufficient balance to gamble (logic error).");
                 }

                 bool win = RngService.GetNextDouble(session.Seed, (int)DateTime.UtcNow.Ticks) > 0.5;
                 
                 // Rigging check (Brain) - prevent user from doubling too much
                 if (state.PendingGambleWin > 1000) win = false; 

                 if (win) {
                     decimal newWin = state.PendingGambleWin * 2;
                     state.PendingGambleWin = newWin;
                     VaultService.ProcessWin(userId, newWin, repo);
                     questService.UpdateProgress(userId, "WinAmount", (int)newWin, repo, VaultService);
                 } else {
                     state.PendingGambleWin = 0;
                     state.IsGambleActive = false; // Game over
                 }
             }

             _cache.Set(cacheKey, state, TimeSpan.FromMinutes(10));
             session.GameState = JsonSerializer.Serialize(state);
             repo.SaveChanges();
             
             await RealTimeService.NotifyGameUpdate(userId, new { GambleResult = state.PendingGambleWin, IsGambleActive = state.IsGambleActive });
        });
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) => new Outcome { GameRoundId = roundId };
    public override async Task<object?> GetCurrentState(Guid sessionId) => null;
}