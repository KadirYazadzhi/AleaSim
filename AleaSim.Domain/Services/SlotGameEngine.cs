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

    // Symbol Constants
    private const int SYM_CLOVER = 8;
    private const int SYM_SEVEN = 7;
    
    // Bonus Phase Symbols
    private const int SYM_BELL_CASH = 10;
    private const int SYM_BELL_MINI = 11;
    private const int SYM_BELL_MINOR = 12;
    private const int SYM_BLANK = 0; 

    // New Advanced Symbols
    private const int SYM_GOLDEN_CLOVER = 13; // Super Trigger
    private const int SYM_COIN = 14;          // Respin Collect

    private readonly int[] _baseSymbols = { 1, 2, 3, 4, 5, 6, SYM_SEVEN, SYM_CLOVER };
    // Golden Clover is rare, injected via logic, not random base fill usually

    // 15 Fixed Paylines
    private static readonly int[][] _paylines = new[] {
        new[] { 1, 1, 1, 1, 1 }, // 1
        new[] { 0, 0, 0, 0, 0 }, // 2
        new[] { 2, 2, 2, 2, 2 }, // 3
        new[] { 3, 3, 3, 3, 3 }, // 4
        new[] { 0, 1, 2, 1, 0 }, // 5
        new[] { 2, 1, 0, 1, 2 }, // 6
        new[] { 1, 0, 0, 0, 1 }, // 7
        new[] { 1, 2, 2, 2, 1 }, // 8
        new[] { 0, 0, 1, 2, 2 }, // 9
        new[] { 2, 2, 1, 0, 0 }, // 10
        new[] { 0, 1, 1, 1, 0 }, // 11
        new[] { 2, 1, 1, 1, 2 }, // 12
        new[] { 0, 1, 0, 1, 0 }, // 13
        new[] { 2, 1, 2, 1, 2 }, // 14
        new[] { 1, 1, 0, 1, 1 }  // 15
    };

    // State for Persistence
    public class CloverChaseState {
        public int[,] Grid { get; set; } = new int[Rows, Cols];
        
        // Respin State
        public bool IsRespinActive { get; set; }
        public int RespinLives { get; set; }
        public decimal LockedBet { get; set; }
        
        // Bonus State
        public bool IsBonusActive { get; set; }
        public bool IsSuperBonus { get; set; } // Triggered by Golden Clover
        public int BonusLives { get; set; }
        public List<BonusSymbol> BonusSymbols { get; set; } = new();
        
        // Gamble State
        public decimal PendingGambleAmount { get; set; }
        public bool CanGamble { get; set; }
    }

    public class BonusSymbol {
        public int Row { get; set; }
        public int Col { get; set; }
        public int Type { get; set; } 
        public decimal Value { get; set; }
    }

    // Paytable
    private static readonly Dictionary<int, Dictionary<int, decimal>> _paytable = new() {
        { 1, new() { {3, 0.2m}, {4, 1m}, {5, 2m} } },
        { 2, new() { {3, 0.2m}, {4, 1m}, {5, 2m} } },
        { 3, new() { {3, 0.5m}, {4, 2m}, {5, 5m} } },
        { 4, new() { {3, 0.5m}, {4, 2m}, {5, 5m} } },
        { 5, new() { {3, 1m}, {4, 5m}, {5, 10m} } },
        { 6, new() { {3, 1m}, {4, 5m}, {5, 10m} } },
        { SYM_SEVEN, new() { {3, 2m}, {4, 10m}, {5, 50m} } },
        { SYM_CLOVER, new() { {3, 5m}, {4, 25m}, {5, 100m} } },
        { SYM_GOLDEN_CLOVER, new() { {3, 10m}, {4, 50m}, {5, 200m} } } // Pays more!
    };
    
    public SlotGameEngine(IRngService rngService, IVaultService vaultService, IBrainService brainService, IPromotionService promotionService, IJackpotService jackpotService, IRealTimeService realTimeService, IServiceScopeFactory scopeFactory) 
        : base(rngService, vaultService, brainService, promotionService, jackpotService, realTimeService, scopeFactory) {
    }

    public override async Task PlaceBet(Guid sessionId, decimal amount, string betData) {
        // Check state to see if this is a Free Spin
        await ExecuteScopedAsync(async repo => {
            var session = repo.GetSession(sessionId);
            
            // Feature Buy Logic
            bool isFeatureBuy = false;
            decimal cost = amount;

            if (!string.IsNullOrEmpty(betData) && betData.Contains("\"IsFeatureBuy\":true")) {
                isFeatureBuy = true;
                cost = amount * 100; // Feature Buy costs 100x bet
            }

            if (session != null && !string.IsNullOrEmpty(session.GameState)) {
                var state = JsonSerializer.Deserialize<CloverChaseState>(session.GameState);
                // If in Bonus (Free Spins), do NOT charge.
                if (state != null && state.IsBonusActive) {
                    return; 
                }
            }
            
            // If Feature Buy, we charge 100x but record the bet as 1x for payout calculations
            if (isFeatureBuy) {
                // Manually charge vault 100x
                bool fundsDeducted = VaultService.ProcessBet(session.UserId, cost, repo);
                if (!fundsDeducted) throw new InvalidOperationException("Insufficient funds for Feature Buy.");
                
                // Record Bet as standard amount (so payouts are correct multiplier)
                // But we need to flag it as Feature Buy for ResolveRound to trigger bonus
                var bet = new Bet {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    UserId = session.UserId,
                    Amount = amount, // Base bet for math
                    BetData = betData, // Contains IsFeatureBuy flag
                    CreatedAt = DateTime.UtcNow
                };
                repo.SaveBet(bet);
                
                // Track Statistics (Total Wagered includes the full cost)
                BrainService.UpdateProfile(session.UserId, cost, 0); 
                PromotionService.ProcessBetActivity(session.UserId, cost, repo);
                await JackpotService.Contribute(session.GameId, cost, repo);
            } 
            else {
                // Standard Bet
                await base.PlaceBet(sessionId, amount, betData);
            }
        });
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        // Use the new overload providing ServiceProvider
        return await ExecuteScopedAsync(async (repo, provider) => {
            using var transaction = repo.BeginTransaction();
            try {
                var session = repo.GetSession(sessionId);
                if (session == null) throw new InvalidOperationException("Session not found.");

                // Load State
                CloverChaseState state;
                if (!string.IsNullOrEmpty(session.GameState)) {
                    state = JsonSerializer.Deserialize<CloverChaseState>(session.GameState) ?? new CloverChaseState();
                } else {
                    state = new CloverChaseState();
                }

                // If user spins while gamble is pending, they collect the win automatically
                if (state.CanGamble) {
                    state.CanGamble = false; 
                    state.PendingGambleAmount = 0;
                }

                var lastBet = repo.GetLastBet(sessionId);
                // Feature Buy Check
                bool isFeatureBuy = lastBet?.BetData?.Contains("\"IsFeatureBuy\":true") ?? false;

                // FIX: If Free Spin, look for the Locking Bet.
                decimal betAmount = state.IsBonusActive || state.IsRespinActive ? state.LockedBet : (lastBet?.Amount ?? 0);
                
                if (betAmount == 0 && !state.IsBonusActive) throw new InvalidOperationException("No bet found.");

                decimal winAmount = 0;
                bool isFreeSpin = state.IsBonusActive;
                bool nudgeTriggered = false;
                string shadowResult = "";

                // --- MAIN LOGIC FLOW ---
                if (isFeatureBuy && !state.IsBonusActive && !state.IsRespinActive) {
                    // FORCE BONUS ENTRY
                    state.IsBonusActive = true;
                    state.BonusLives = 3;
                    state.LockedBet = betAmount;
                    // Visuals: Show 5 clovers immediately
                    for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) state.Grid[r,c] = _baseSymbols[0]; // Fill junk
                    // Place 5 Clovers random
                    var rnd = new Random();
                    for(int i=0; i<5; i++) state.Grid[rnd.Next(Rows), i] = SYM_CLOVER;
                    
                    TransformCloversToBells(state, betAmount);
                    // No payout for the trigger spin usually, just entry
                    winAmount = 0;
                }
                else if (state.IsBonusActive) {
                    PlayBonusSpin(state, betAmount, session.Seed);
                    
                    if (state.BonusLives <= 0 || state.BonusSymbols.Count >= Rows * Cols) {
                        winAmount = CalculateBonusWin(state, betAmount);
                        state.IsBonusActive = false;
                        state.IsSuperBonus = false;
                        state.BonusSymbols.Clear();
                    }
                }
                else if (state.IsRespinActive) {
                    // Bet already processed in PlaceBet (because Respin is paid)
                    // Special: Pass out var for Coin Wins
                    decimal coinWins = PlayRespin(state, session.Seed, betAmount, out nudgeTriggered);
                    
                    // Coin wins are paid immediately
                    winAmount = CalculateTotalWin(state.Grid, betAmount / 15m) + coinWins;
                    
                    // Check for Bonus Trigger (5+ Clovers/Gold)
                    int clovers = CountClovers(state.Grid);
                    if (clovers >= 5) {
                        state.IsRespinActive = false;
                        state.IsBonusActive = true;
                        state.BonusLives = 3;
                        // Check for Golden Clover
                        state.IsSuperBonus = CheckGoldenClover(state.Grid);
                        TransformCloversToBells(state, betAmount);
                    }
                }
                else {
                    // Base Game Spin
                    // Bet already processed in PlaceBet
                    var decision = BrainService.DecideOutcome(session.UserId, session.GameId, betAmount);
                    
                    // --- SHADOW MODE (Parallel Testing) ---
                    var shadowDecision = BrainService.DecideOutcome(session.UserId, session.GameId, betAmount, isShadowMode: true);
                    shadowResult = JsonSerializer.Serialize(shadowDecision);

                    PlayBaseSpin(state, decision, betAmount, session.Seed);
                    winAmount = CalculateTotalWin(state.Grid, betAmount / 15m);

                    int clovers = CountClovers(state.Grid);
                    if (clovers >= 5) {
                        state.IsBonusActive = true;
                        state.BonusLives = 3;
                        state.LockedBet = betAmount;
                        state.IsSuperBonus = CheckGoldenClover(state.Grid);
                        TransformCloversToBells(state, betAmount);
                    }
                    else if (clovers > 0) {
                        state.IsRespinActive = true;
                        state.RespinLives = 3;
                        state.LockedBet = betAmount;
                    }
                }

                // --- PAYOUT & GAMBLE ---
                if (winAmount > 0) {
                    VaultService.ProcessWin(session.UserId, winAmount, repo);
                    BrainService.UpdateProfile(session.UserId, lastBet.Amount, winAmount);
                    
                    // Enable Gamble if it's a Base/Respin win (not Bonus completion)
                    if (!state.IsBonusActive && !isFreeSpin) {
                        state.CanGamble = true;
                        state.PendingGambleAmount = winAmount;
                    }
                } else {
                     BrainService.UpdateProfile(session.UserId, lastBet.Amount, 0);
                     state.CanGamble = false;
                }
                
                PromotionService.ProcessWinActivity(session.UserId, winAmount, repo);
                
                // --- SOCIAL: LEADERBOARD & QUESTS ---
                try {
                    // Quests
                    var questService = provider.GetService<IQuestService>();
                    if (questService != null) {
                        questService.GenerateDailyQuests(session.UserId, repo);
                        questService.UpdateProgress(session.UserId, "SpinCount", 1, repo, VaultService);
                        if (winAmount > 0) {
                            questService.UpdateProgress(session.UserId, "WinAmount", (int)winAmount, repo, VaultService);
                        }
                    }

                    // Leveling (RPG)
                    var levelService = provider.GetService<ILevelService>();
                    if (levelService != null) {
                        // Base XP logic is handled inside service
                        levelService.AddExperience(session.UserId, lastBet.Amount, repo, RealTimeService);
                    }

                    // Leaderboard
                    if (winAmount > 0) {
                        var leaderboardService = provider.GetService<ILeaderboardService>();
                        if (leaderboardService != null) {
                            var user = repo.GetUser(session.UserId);
                            if (user != null) {
                                leaderboardService.SubmitScore(session.UserId, user.Username, winAmount, lastBet.Amount, "Clover Chase");
                            }
                        }
                    }
                }
                catch {
                     // Social failures should not break the game round
                }

                session.GameState = JsonSerializer.Serialize(state);
                
                var round = new GameRound {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    RoundNumber = repo.GetRoundCount(sessionId) + 1,
                    RandomResult = JsonSerializer.Serialize(new { Grid = FlattenGrid(state.Grid), BonusSymbols = state.BonusSymbols, Nudge = nudgeTriggered, CoinWin = winAmount }), 
                    TotalBetAmount = isFreeSpin ? 0 : lastBet.Amount,
                    TotalWinAmount = winAmount,
                    DecisionType = isFreeSpin ? "FreeSpin" : (state.IsRespinActive ? "Respin" : "Base"),
                    ShadowBrainResult = shadowResult,
                    ExecutedAt = DateTime.UtcNow
                };

                repo.SaveRound(round);
                lastBet.GameRoundId = round.Id;
                repo.UpdateBet(lastBet);
                repo.SaveOutcome(new Outcome { Id = Guid.NewGuid(), GameRoundId = round.Id, WinAmount = winAmount });
                
                transaction.Commit();

                await RealTimeService.NotifyGameUpdate(session.UserId, new { 
                    SessionId = sessionId, 
                    Game = "CloverChase", 
                    Grid = FlattenGrid(state.Grid), 
                    Win = winAmount,
                    State = state,
                    Nudge = nudgeTriggered
                });

                return round;
            }
            catch {
                transaction.Rollback();
                throw;
            }
        });
    }

    // --- INTERACTIVE ACTIONS (GAMBLE) ---
    public override async Task ProcessAction(Guid sessionId, string action, string actionData) {
        await ExecuteScopedAsync(async repo => {
            var session = repo.GetSession(sessionId);
            if (session == null) return;
            
            // Load State
            if (string.IsNullOrEmpty(session.GameState)) return;
            var state = JsonSerializer.Deserialize<CloverChaseState>(session.GameState);
            if (state == null || !state.CanGamble) return; // Cannot gamble

            if (action.ToLower() == "gamble") {
                // actionData: "red" or "black"
                bool win = new Random().NextDouble() > 0.5; // 50/50
                
                if (win) {
                    decimal winAmount = state.PendingGambleAmount * 2;
                    // Vault: Debit original win (already paid) and credit double? 
                    // No, just credit the difference (another 1x).
                    VaultService.ProcessWin(session.UserId, state.PendingGambleAmount, repo); 
                    
                    state.PendingGambleAmount = winAmount; // Can gamble again? Limit to 5 in UI.
                    // For now, let's auto-collect or allow loop? Assuming loop allowed.
                } else {
                    // Loss: Debit the original win amount from user balance?
                    // Tricky: We already credited it in ResolveRound.
                    // To "Lose" it, we must deduct it.
                    VaultService.ProcessBet(session.UserId, state.PendingGambleAmount, repo);
                    state.PendingGambleAmount = 0;
                    state.CanGamble = false;
                }
                
                session.GameState = JsonSerializer.Serialize(state);
                repo.SaveChanges(); // Need to save session state update

                await RealTimeService.NotifyGameUpdate(session.UserId, new { 
                    Type = "GambleResult", 
                    Win = win, 
                    NewAmount = state.PendingGambleAmount 
                });
            }
            else if (action.ToLower() == "collect") {
                state.CanGamble = false;
                state.PendingGambleAmount = 0;
                session.GameState = JsonSerializer.Serialize(state);
                repo.SaveChanges();
            }
        });
    }

    // --- CORE LOGIC ---

    private void PlayBaseSpin(CloverChaseState state, BrainDirective decision, decimal betAmount, int seed) {
        if (decision.TargetWinAmount > 0) {
            decimal actualWin;
            state.Grid = ConstructGridForWin(decision.TargetWinAmount, betAmount / 15m, out actualWin);
        } else if (decision.IsNearMiss) {
            // Personalized Near Miss
            state.Grid = ConstructNearMissGrid(decision.PreferredNearMissSymbol ?? SYM_SEVEN, betAmount / 15m);
        } else {
            // Losing Spin
            for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) state.Grid[r,c] = -1;
            FillJunkSafely(state.Grid, betAmount/15m, 0);
        }
        
        // Ensure no empty spots left
        var rnd = new Random(seed);
        for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) 
            if (state.Grid[r,c] == -1) state.Grid[r,c] = _baseSymbols[rnd.Next(_baseSymbols.Length)];
    }

    private int[,] ConstructNearMissGrid(int favoriteSymbol, decimal betPerLine) {
        int[,] grid = new int[Rows, Cols];
        for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) grid[r,c] = -1;

        // Place 2 favorites on Line 1 (Columns 0 and 1)
        grid[_paylines[0][0], 0] = favoriteSymbol;
        grid[_paylines[0][1], 1] = favoriteSymbol;
        
        // Place one favorite just OFF the line on Column 2
        int targetRow = _paylines[0][2];
        int nearMissRow = (targetRow == 0) ? 1 : targetRow - 1;
        grid[nearMissRow, 2] = favoriteSymbol;

        // Block the actual win line on Col 2
        grid[targetRow, 2] = GetSafeBlocker(favoriteSymbol);

        FillJunkSafely(grid, betPerLine, 0);
        return grid;
    }

    private int[,] ConstructGridForWin(decimal targetWin, decimal betPerLine, out decimal actualWin) {
        decimal targetMultiplier = targetWin / betPerLine;
        int[,] grid = new int[Rows, Cols];
        for(int r=0; r<Rows; r++) for(int c=0; c<Cols; c++) grid[r,c] = -1;
        
        // --- ADVANCED MULTI-LINE SOLVER ---
        // We try to solve: Sum(Line_Multiplier) = TargetMultiplier
        // We use a simplified greedy approach for this prototype.
        
        decimal remainingMultiplier = targetMultiplier;
        var usedLines = new List<int>();
        
        // 1. Pick a high-value line first
        int primaryLine = 0; // Always Line 1 for simplicity in positioning
        
        // Find best symbol for primary line
        int bestSym = 1;
        int bestCount = 3;
        decimal bestPay = 0;

        foreach (var sym in _paytable.OrderByDescending(x => x.Key)) {
            foreach (var pay in sym.Value.OrderByDescending(x => x.Value)) {
                if (pay.Value <= remainingMultiplier) {
                    bestSym = sym.Key;
                    bestCount = pay.Key;
                    bestPay = pay.Value;
                    goto FoundPrimary;
                }
            }
        }
        FoundPrimary:
        
        // Place primary win
        for (int i = 0; i < bestCount; i++) grid[_paylines[primaryLine][i], i] = bestSym;
        if (bestCount < Cols) grid[_paylines[primaryLine][bestCount], bestCount] = GetSafeBlocker(bestSym);
        
        remainingMultiplier -= bestPay;
        usedLines.Add(primaryLine);

        // 2. Try to fill remaining with secondary lines if possible
        if (remainingMultiplier >= 0.2m) { // Minimum paytable value
            // Look for another line that doesn't conflict too much with Line 1
            // conflicting = sharing same (column, row) but requiring different symbols
            for (int l = 1; l < _paylines.Length; l++) {
                if (remainingMultiplier < 0.2m) break;

                // Simple check: Does this line share spots with already set spots?
                bool conflict = false;
                for (int i = 0; i < Cols; i++) {
                    int r = _paylines[l][i];
                    if (grid[r, i] != -1) {
                        // Spot is occupied. Can we use it?
                        // Only if we need the same symbol there. 
                        // For simplicity, if it's occupied by anything, we consider it a constraint.
                        // We'll only use lines that don't overwrite our primary win.
                    }
                }

                // To keep it robust, let's find a symbol that fits the remaining multiplier
                foreach (var sym in _paytable) {
                    foreach (var pay in sym.Value) {
                        if (Math.Abs(pay.Value - remainingMultiplier) < 0.01m) { // Exact match found!
                            // Try to place this on line 'l'
                            if (CanPlaceOnLine(grid, l, pay.Key, sym.Key)) {
                                PlaceOnLine(grid, l, pay.Key, sym.Key);
                                remainingMultiplier -= pay.Value;
                                goto NextRemaining;
                            }
                        }
                    }
                }
                NextRemaining:;
            }
        }

        // 3. Final cleanup and safety
        FillJunkSafely(grid, betPerLine, 0); 
        
        actualWin = CalculateTotalWin(grid, betPerLine);
        return grid;
    }

    private bool CanPlaceOnLine(int[,] grid, int lineIdx, int count, int symbol) {
        int[] line = _paylines[lineIdx];
        for (int i = 0; i < count; i++) {
            int r = line[i];
            if (grid[r, i] != -1 && grid[r, i] != symbol && !IsClover(grid[r,i])) return false;
        }
        // Block next
        if (count < Cols) {
            int rBlock = line[count];
            if (grid[rBlock, count] == symbol) return false; // Already has it, would increase win count
        }
        return true;
    }

    private void PlaceOnLine(int[,] grid, int lineIdx, int count, int symbol) {
        int[] line = _paylines[lineIdx];
        for (int i = 0; i < count; i++) {
            grid[line[i], i] = symbol;
        }
        if (count < Cols) {
            grid[line[count], count] = GetSafeBlocker(symbol);
        }
    }

    private void FillJunkSafely(int[,] grid, decimal betPerLine, decimal currentWinBase) {
        var rnd = new Random();
        
        for (int c = 0; c < Cols; c++) {
            for (int r = 0; r < Rows; r++) {
                if (grid[r, c] != -1) continue; // Already set

                int chosenSym = -1;
                bool valid = false;
                int attempts = 0;

                while (!valid && attempts < 5) {
                    if (attempts == 0 && c > 0 && rnd.NextDouble() < 0.4) {
                        chosenSym = grid[r, c-1]; // Clustering
                    } else {
                        do {
                            chosenSym = _baseSymbols[rnd.Next(_baseSymbols.Length)];
                        } while (IsClover(chosenSym)); // Avoid accidental triggers in junk
                    }

                    if (IsSafePlacement(grid, r, c, chosenSym)) {
                        valid = true;
                    }
                    attempts++;
                }

                if (!valid) {
                    grid[r, c] = GetSafeBlocker(c > 0 ? grid[r, c-1] : 1);
                } else {
                    grid[r, c] = chosenSym;
                }
            }
        }
    }

    private bool IsSafePlacement(int[,] grid, int r, int c, int symbol) {
        if (c < 2) return true;

        for (int i = 0; i < _paylines.Length; i++) {
            int[] line = _paylines[i];
            if (line[c] != r) continue;

            int sym1 = grid[line[c-1], c-1];
            int sym2 = grid[line[c-2], c-2];
            
            // If previous spots are empty (-1), safe
            if (sym1 == -1 || sym2 == -1) continue;

            if (IsMatch(sym1, symbol) && IsMatch(sym2, symbol)) {
                return false; 
            }
        }
        return true;
    }

    private bool IsMatch(int s1, int s2) {
        if (s1 == s2) return true;
        if (IsClover(s1) || IsClover(s2)) return true;
        if ((s1 == SYM_SEVEN && s2 <= 6) || (s2 == SYM_SEVEN && s1 <= 6)) return true;
        return false;
    }

    private int GetSafeBlocker(int neighborSym) {
        return (neighborSym == 1) ? 2 : 1;
    }

    private decimal PlayRespin(CloverChaseState state, int seed, decimal betAmount, out bool nudgeTriggered) {
        nudgeTriggered = false;
        int oldClovers = CountClovers(state.Grid);
        var rnd = new Random(seed);
        decimal coinWin = 0;

        for(int r=0; r<Rows; r++) {
            for(int c=0; c<Cols; c++) {
                if (!IsClover(state.Grid[r,c])) {
                    // Spin empty cells
                    // Inject COINS here!
                    double roll = rnd.NextDouble();
                    if (roll < 0.05) { // 5% chance for Coin
                        state.Grid[r,c] = SYM_COIN;
                        coinWin += betAmount; // Coin pays 1x Bet
                    } else {
                        state.Grid[r,c] = _baseSymbols[rnd.Next(_baseSymbols.Length)];
                    }
                }
            }
        }
        
        int newClovers = CountClovers(state.Grid);
        
        // MYSTERY NUDGE: If we have 4 clovers, lives are running out, try to find a clover off-screen
        if (newClovers == 4 && state.RespinLives <= 1 && newClovers == oldClovers) {
            // Fake the Nudge: Just force a clover on a non-clover reel if RNG allows
            // In a real physics engine, we check virtual strip. Here we cheat for player benefit.
            if (rnd.NextDouble() < 0.3) { // 30% chance to save
                // Find a column without clover
                for(int c=0; c<Cols; c++) {
                    bool hasClover = false;
                    for(int r=0; r<Rows; r++) if (IsClover(state.Grid[r,c])) hasClover = true;
                    
                    if (!hasClover) {
                        state.Grid[0,c] = SYM_CLOVER; // Nudge it to top row
                        nudgeTriggered = true;
                        newClovers++;
                        break;
                    }
                }
            }
        }

        if (newClovers > oldClovers) state.RespinLives = 3;
        else state.RespinLives--;

        if (state.RespinLives <= 0) state.IsRespinActive = false;
        
        return coinWin;
    }

    private void TransformCloversToBells(CloverChaseState state, decimal totalBet) {
        state.BonusSymbols.Clear();
        decimal multiplierBase = state.IsSuperBonus ? 5m : 0.2m; // Super start higher

        for(int r=0; r<Rows; r++) {
            for(int c=0; c<Cols; c++) {
                if (IsClover(state.Grid[r,c])) {
                    // Create Bell
                    decimal val = totalBet * (multiplierBase + (decimal)(new Random().NextDouble() * 5)); 
                    state.BonusSymbols.Add(new BonusSymbol { 
                        Row = r, Col = c, Type = SYM_BELL_CASH, Value = Math.Round(val, 2) 
                    });
                    state.Grid[r,c] = SYM_BELL_CASH; 
                } else {
                    state.Grid[r,c] = SYM_BLANK;
                }
            }
        }
    }

    private void PlayBonusSpin(CloverChaseState state, decimal totalBet, int seed) {
        var rnd = new Random(seed);
        bool landedNew = false;
        int minis = state.BonusSymbols.Count(s => s.Type == SYM_BELL_MINI);
        int minors = state.BonusSymbols.Count(s => s.Type == SYM_BELL_MINOR);

        for(int r=0; r<Rows; r++) {
            for(int c=0; c<Cols; c++) {
                if (state.Grid[r,c] == SYM_BLANK) {
                    if (rnd.NextDouble() < 0.05) {
                        landedNew = true;
                        double typeRoll = rnd.NextDouble();
                        double minorChance = 0.01 / (minors + 1); 
                        double miniChance = 0.05 / (minis + 1);

                        if (typeRoll < minorChance && minors < 3) AddBonusSymbol(state, r, c, SYM_BELL_MINOR, 100m);
                        else if (typeRoll < miniChance && minis < 5) AddBonusSymbol(state, r, c, SYM_BELL_MINI, 20m);
                        else {
                            decimal mult = (decimal)(rnd.NextDouble() * 10);
                            if (state.IsSuperBonus) mult += 5; // Super Bonus boost
                            AddBonusSymbol(state, r, c, SYM_BELL_CASH, Math.Round(totalBet * mult, 2));
                        }
                    }
                }
            }
        }

        if (landedNew) state.BonusLives = 3;
        else state.BonusLives--;
    }

    private void AddBonusSymbol(CloverChaseState state, int r, int c, int type, decimal value) {
        state.BonusSymbols.Add(new BonusSymbol { Row = r, Col = c, Type = type, Value = value });
        state.Grid[r,c] = type;
    }

    private decimal CalculateBonusWin(CloverChaseState state, decimal totalBet) {
        decimal total = 0;
        
        // 1. Sum values + Apply Column Multipliers (Progressive)
        for (int c = 0; c < Cols; c++) {
            // Check if column is full
            int bellsInCol = state.BonusSymbols.Count(s => s.Col == c);
            decimal colMultiplier = (bellsInCol == Rows) ? 2m : 1m; // x2 for full column
            
            var colSymbols = state.BonusSymbols.Where(s => s.Col == c);
            foreach(var s in colSymbols) {
                total += s.Value * colMultiplier;
            }
        }
        
        // 2. Global Multipliers
        int count = state.BonusSymbols.Count;
        if (count >= 20) total *= 3; // Full Screen
        else if (count >= 15) total *= 2;

        return total;
    }

    private bool IsClover(int symbol) {
        return symbol == SYM_CLOVER || symbol == SYM_GOLDEN_CLOVER;
    }

    private bool CheckGoldenClover(int[,] grid) {
        foreach (int val in grid) if (val == SYM_GOLDEN_CLOVER) return true;
        return false;
    }

    private int CountClovers(int[,] grid) {
        int count = 0;
        foreach (int val in grid) if (IsClover(val)) count++;
        return count;
    }

    private decimal CalculateTotalWin(int[,] grid, decimal betPerLine) {
        decimal total = 0;
        foreach (var line in _paylines) {
            int[] lineSyms = new int[Cols];
            for(int c=0; c<Cols; c++) lineSyms[c] = grid[line[c], c];

            int count = 0;
            int? matchSym = null;

            for (int c = 0; c < Cols; c++) {
                int s = lineSyms[c];
                if (matchSym == null) {
                    if (IsClover(s)) count++;
                    else { matchSym = s; count++; }
                } else {
                    bool isMatch = (s == matchSym) || 
                                   IsClover(s) || 
                                   (s == SYM_SEVEN && matchSym.Value <= 6); 
                    if (isMatch) count++;
                    else break;
                }
            }

            int finalSym = matchSym ?? SYM_CLOVER;
            if (count >= 3 && _paytable.ContainsKey(finalSym) && _paytable[finalSym].ContainsKey(count)) {
                total += _paytable[finalSym][count] * betPerLine;
            }
        }
        return total;
    }

    public override async Task<Outcome> GetOutcome(Guid roundId) {
        return await Task.Run(() => ExecuteScoped(repo => repo.GetOutcome(roundId) 
               ?? new Outcome { Id = Guid.NewGuid(), GameRoundId = roundId, ResultJson = "{}" }));
    }

    public override async Task<object?> GetCurrentState(Guid sessionId) {
        return await Task.Run(() => ExecuteScoped<object?>(repo => {
            var session = repo.GetSession(sessionId);
            if (session == null || string.IsNullOrEmpty(session.GameState)) return null;
            return JsonSerializer.Deserialize<CloverChaseState>(session.GameState);
        }));
    }

    private int[][] FlattenGrid(int[,] grid) {
        int[][] flat = new int[Rows][];
        for (int r = 0; r < Rows; r++) {
            flat[r] = new int[Cols];
            for (int c = 0; c < Cols; c++) flat[r][c] = grid[r, c];
        }
        return flat;
    }
}
