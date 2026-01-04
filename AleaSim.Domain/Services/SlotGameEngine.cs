using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using AleaSim.Domain.Models;

namespace AleaSim.Domain.Services;

public class SlotGameEngine : BaseGameEngine {
    private const int Rows = 4;
    private const int Cols = 5;
    private const int PaylinesCount = 20;
    
    // Symbol IDs
    private const int SYM_WILD_CLOVER = 8; // Universal Wild + Trigger
    private const int SYM_WILD_SEVEN = 7;  // Wild for Fruits
    private const int SYM_BELL = 10;       // Bonus Symbol (Base value)
    private const int SYM_GOLDEN_CLOVER = 12; // Super Trigger (Rare)
    
    // Reel Strips (Simplified for Probability)
    // Heavier on low symbols (1-4), lighter on High (7, 8). Added 12 (Golden) very rarely.
    private readonly int[] _reelStrip = { 
        1, 2, 3, 4, 5, 1, 2, 3, 4, 6, 1, 2, 3, 5, 1, 2, 7, 1, 3, 4, 8, 1, 2, 3, 4, 5, 1, 2, 8, 3, 4, 12 
    };

    // Paylines (5x4 Standard)
    private readonly int[][] _paylines = new int[][] {
        new[] {0,0,0,0,0}, new[] {1,1,1,1,1}, new[] {2,2,2,2,2}, new[] {3,3,3,3,3}, // Straights
        new[] {0,1,2,1,0}, new[] {1,2,3,2,1}, new[] {2,1,0,1,2}, new[] {3,2,1,2,3}, // V and ^
        new[] {0,1,0,1,0}, new[] {1,0,1,0,1}, new[] {2,3,2,3,2}, new[] {3,2,3,2,3}, // ZigZags
        new[] {0,0,1,0,0}, new[] {1,1,2,1,1}, new[] {2,2,3,2,2}, new[] {1,1,0,1,1}, // Small Bumps
        new[] {2,2,1,2,2}, new[] {1,0,0,0,1}, new[] {2,3,3,3,2}, new[] {0,1,1,1,0}  // Mixed
    };

    // Paytable (3, 4, 5 matches multipliers)
    private readonly Dictionary<int, decimal[]> _paytable = new() {
        { 1, new[] { 0.2m, 0.5m, 2.0m } }, // Cherry
        { 2, new[] { 0.2m, 0.5m, 2.0m } }, // Lemon
        { 3, new[] { 0.4m, 1.0m, 4.0m } }, // Orange
        { 4, new[] { 0.5m, 1.5m, 5.0m } }, // Plum
        { 5, new[] { 1.0m, 3.0m, 10.0m } }, // Grapes
        { 6, new[] { 1.5m, 5.0m, 15.0m } }, // Watermelon
        { 7, new[] { 2.5m, 10.0m, 25.0m } }, // Red Seven
        { 8, new[] { 5.0m, 20.0m, 50.0m } }  // Clover (Line Pay)
    };

    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public SlotGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {
    }

    public enum BellType { Cash, Mini, Minor, Major }

    public class SlotState {
        public int[][] Grid { get; set; } = new int[Rows][];
        public bool IsRespinActive { get; set; }
        public int RespinLives { get; set; }
        public List<Point> StickyClovers { get; set; } = new();
        public bool HasGoldenClover { get; set; } // Tracks if Super Bonus is active
        public bool IsBonusActive { get; set; } // Hold & Win Phase
        public int BonusLives { get; set; }
        public decimal LockedBet { get; set; }
        public List<BellValue> BonusBells { get; set; } = new(); // For Bonus Phase
        
        public SlotState() {
            for(int r=0; r<Rows; r++) Grid[r] = new int[Cols];
        }
    }

    public class Point { public int R { get; set; } public int C { get; set; } }
    public class BellValue { 
        public Point Pos { get; set; } = new(); 
        public decimal Value { get; set; } 
        public BellType Type { get; set; } = BellType.Cash;
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async (repo, questService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");

            SlotState state = string.IsNullOrEmpty(session.GameState) 
                ? new SlotState() 
                : JsonSerializer.Deserialize<SlotState>(session.GameState) ?? new SlotState();

            // 2. Bet Logic (Locking)
            var lastBet = repo.GetLastBet(sessionId);
            decimal currentBet = lastBet?.Amount ?? 1.0m;

            if (state.IsRespinActive || state.IsBonusActive) {
                currentBet = state.LockedBet;
                if (state.IsRespinActive && !state.IsBonusActive) {
                    if (!VaultService.ProcessBet(session.UserId, currentBet, repo)) {
                        throw new Exception("Insufficient funds for Respin.");
                    }
                    repo.SaveBet(new Bet { Id = Guid.NewGuid(), GameSessionId = sessionId, UserId = session.UserId, Amount = currentBet, CreatedAt = DateTime.UtcNow });
                    BrainService.UpdateProfile(session.UserId, currentBet, 0); 
                }
            } else {
                state.LockedBet = currentBet;
                state.HasGoldenClover = false; 
            }

            var directive = BrainService.DecideOutcome(session.UserId, GameId, currentBet, repo);
            
            decimal winAmount = 0;
            int attempts = 0;
            const int MAX_ATTEMPTS = 50; 

            do {
                attempts++;
                
                if (state.IsBonusActive) {
                    PlayBonusRound(state, session.Seed + attempts);
                } 
                else {
                    PlayStandardRound(state, session.Seed + attempts);
                }

                if (state.IsBonusActive) {
                    winAmount = 0; 
                    if (state.BonusLives == 0 || state.BonusBells.Count == Rows * Cols) {
                        winAmount = state.BonusBells.Sum(b => b.Value);
                        if (state.BonusBells.Count == Rows * Cols) winAmount *= 2; 
                        
                        state.IsBonusActive = false; 
                        state.IsRespinActive = false;
                        state.StickyClovers.Clear();
                        state.HasGoldenClover = false;
                    }
                } 
                else {
                    winAmount = EvaluateGrid(state.Grid, currentBet);
                }

                bool isAcceptable = CheckBrainCompliance(directive, winAmount, currentBet);
                if (state.IsRespinActive || state.IsBonusActive) isAcceptable = true; 

                if (isAcceptable) break;

            } while (attempts < MAX_ATTEMPTS);

            if (winAmount > 0) {
                VaultService.ProcessWin(session.UserId, winAmount, repo);
                questService.UpdateProgress(session.UserId, "WinAmount", (int)winAmount, repo, VaultService);
            }
            
            if (!state.IsRespinActive && !state.IsBonusActive) {
                BrainService.UpdateProfile(session.UserId, 0, winAmount);
            }

            session.GameState = JsonSerializer.Serialize(state);

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                TotalBetAmount = currentBet,
                TotalWinAmount = winAmount,
                RandomResult = JsonSerializer.Serialize(new { 
                    Grid = state.Grid, 
                    state.IsRespinActive, 
                    state.RespinLives,
                    state.IsBonusActive,
                    state.BonusLives,
                    state.HasGoldenClover,
                    BonusBells = state.BonusBells,
                    CoinWin = winAmount 
                }),
                DecisionType = directive.DecisionType,
                ExecutedAt = DateTime.UtcNow
            };

            repo.SaveRound(round);
            
            await RealTimeService.NotifyGameUpdate(session.UserId, new { 
                Grid = state.Grid, 
                Win = winAmount, 
                IsRespin = state.IsRespinActive,
                Lives = state.IsRespinActive ? state.RespinLives : state.BonusLives 
            });

            return round;
        });
    }

    private void PlayStandardRound(SlotState state, int seed) {
        int nonce = 0;
        var stickyMap = new HashSet<(int,int)>();
        foreach(var p in state.StickyClovers) stickyMap.Add((p.R, p.C));

        for (int c = 0; c < Cols; c++) {
            int stop = RngService.GetNextInt(seed, nonce++, 0, _reelStrip.Length);
            for (int r = 0; r < Rows; r++) {
                if (stickyMap.Contains((r,c))) {
                    state.Grid[r][c] = SYM_WILD_CLOVER; 
                    continue; 
                }
                int symbolIndex = (stop + r) % _reelStrip.Length;
                state.Grid[r][c] = _reelStrip[symbolIndex];
            }
        }

        int newCloversCount = 0;
        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                int sym = state.Grid[r][c];
                if ((sym == SYM_WILD_CLOVER || sym == SYM_GOLDEN_CLOVER) && !stickyMap.Contains((r,c))) {
                    state.StickyClovers.Add(new Point { R = r, C = c });
                    if (sym == SYM_GOLDEN_CLOVER) state.HasGoldenClover = true;
                    state.Grid[r][c] = SYM_WILD_CLOVER; 
                    newCloversCount++;
                }
            }
        }

        if (newCloversCount > 0) {
            if (!state.IsRespinActive) state.IsRespinActive = true;
            state.RespinLives = 3;
        } 
        else if (state.IsRespinActive) {
            state.RespinLives--;
            if (state.RespinLives <= 0) {
                if (state.StickyClovers.Count >= 5) {
                    state.IsBonusActive = true;
                    state.BonusLives = 3;
                    state.IsRespinActive = false;
                    InitializeBonusGrid(state);
                } else {
                    state.IsRespinActive = false;
                    state.StickyClovers.Clear();
                    state.HasGoldenClover = false;
                }
            }
        }
    }

    private void InitializeBonusGrid(SlotState state) {
        state.BonusBells.Clear();
        for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) state.Grid[r][c] = 0;

        foreach(var p in state.StickyClovers) {
            state.Grid[p.R][p.C] = SYM_BELL;
            
            int minMult = state.HasGoldenClover ? 5 : 1;
            int maxMult = state.HasGoldenClover ? 20 : 10;
            
            decimal val = state.LockedBet * (decimal)(new Random().Next(minMult, maxMult));
            
            state.BonusBells.Add(new BellValue { Pos = p, Value = val, Type = BellType.Cash });
        }
    }

    private void PlayBonusRound(SlotState state, int seed) {
        bool newBellLanded = false;
        int nonce = 0;

        for (int r = 0; r < Rows; r++) {
            for (int c = 0; c < Cols; c++) {
                if (state.Grid[r][c] == SYM_BELL) continue;

                if (RngService.GetNextDouble(seed, nonce++) < 0.08) {
                    state.Grid[r][c] = SYM_BELL;
                    newBellLanded = true;
                    
                    double typeRoll = RngService.GetNextDouble(seed, nonce++);
                    BellValue newBell = new BellValue { Pos = new Point { R=r, C=c } };

                    int miniCount = state.BonusBells.Count(b => b.Type == BellType.Mini);
                    int minorCount = state.BonusBells.Count(b => b.Type == BellType.Minor);

                    double chanceMini = 0.05 / Math.Pow(2, miniCount); 
                    double chanceMinor = 0.01 / Math.Pow(4, minorCount); 
                    double chanceMajor = 0.001; 

                    if (typeRoll < chanceMajor) {
                        newBell.Type = BellType.Major;
                        newBell.Value = state.LockedBet * 500m;
                    }
                    else if (typeRoll < chanceMajor + chanceMinor && minorCount < 3) {
                        newBell.Type = BellType.Minor;
                        newBell.Value = state.LockedBet * 50m; 
                    }
                    else if (typeRoll < chanceMajor + chanceMinor + chanceMini) {
                        newBell.Type = BellType.Mini;
                        newBell.Value = state.LockedBet * 20m; 
                    }
                    else {
                        newBell.Type = BellType.Cash;
                        int minMult = state.HasGoldenClover ? 5 : 1;
                        newBell.Value = state.LockedBet * (decimal)(new Random().Next(minMult, 10));
                    }

                    state.BonusBells.Add(newBell);
                }
            }
        }

        if (newBellLanded) state.BonusLives = 3; else state.BonusLives--;
    }

    private decimal EvaluateGrid(int[][] grid, decimal betAmount) {
        decimal totalWin = 0;
        foreach (var line in _paylines) {
            int firstSym = grid[line[0]][0];
            int count = 1;
            int matchSym = firstSym;

            if (matchSym == SYM_WILD_CLOVER || matchSym == SYM_GOLDEN_CLOVER) matchSym = -1;

            for (int c = 1; c < Cols; c++) {
                int r = line[c];
                int current = grid[r][c];

                if (IsMatch(matchSym, current, out int resolvedSym)) {
                    count++;
                    if (matchSym == -1 && resolvedSym != SYM_WILD_CLOVER && resolvedSym != SYM_GOLDEN_CLOVER) matchSym = resolvedSym;
                } else {
                    break;
                }
            }

            if (count >= 3) {
                int lookupSym = (matchSym == -1) ? SYM_WILD_CLOVER : matchSym;
                if (_paytable.ContainsKey(lookupSym)) {
                    totalWin += betAmount * _paytable[lookupSym][count - 3];
                }
            }
        }
        return totalWin;
    }

    private bool IsMatch(int target, int current, out int resolved) {
        resolved = target;
        if (target == -1) { resolved = current; return true; }
        if (target == current) return true;
        if (current == SYM_WILD_CLOVER || current == SYM_GOLDEN_CLOVER) return true; 
        if (current == SYM_WILD_SEVEN && target <= 6) return true;
        if (target == SYM_WILD_SEVEN && current <= 6) return true;
        return false;
    }

    private bool CheckBrainCompliance(BrainDirective d, decimal actualWin, decimal bet) {
        if (d.DecisionType == "Random") return true; 
        if (d.DecisionType == "ForceBonus" && actualWin == 0) return false; 
        if (d.TargetWinAmount == 0 && actualWin == 0) return true; 
        if (d.TargetWinAmount > 0 && actualWin >= d.TargetWinAmount * 0.8m) return true; 
        return false;
    }

    public override Task ProcessAction(Guid sessionId, string action, string actionData) => Task.CompletedTask;
    public override async Task<Outcome> GetOutcome(Guid roundId) => new Outcome { GameRoundId = roundId };
    public override async Task<object?> GetCurrentState(Guid sessionId) => null;
}