using AleaSim.Shared.Models;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class RouletteGameEngine : BaseGameEngine {
    public class RouletteState { public int Nonce { get; set; } }
    private Guid GameId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public RouletteGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, ILockService lockService)
        : base(rng, vault, brain, promo, jackpot, realTime, scope, lockService) {
    }
    public override async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string? betData) {
        var bets = new List<RouletteBetDto>();
        try {
            if (!string.IsNullOrEmpty(betData)) {
                string jsonToParse = betData;
                if (betData.Trim().StartsWith("\"")) {
                    jsonToParse = JsonSerializer.Deserialize<string>(betData) ?? "[]";
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                using var doc = JsonDocument.Parse(jsonToParse);
                if (doc.RootElement.ValueKind == JsonValueKind.Array) {
                    bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(jsonToParse, options) ?? new();
                } else {
                    var property = doc.RootElement.EnumerateObject().FirstOrDefault(p => p.Name.Equals("bets", StringComparison.OrdinalIgnoreCase));
                    if (property.Value.ValueKind == JsonValueKind.Array) {
                        bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(property.Value.GetRawText(), options) ?? new();
                    }
                }
            }
        } catch { throw new Exception("Security Alert: Invalid bet format detected."); }

        // STRICT VALIDATION
        if (bets == null || !bets.Any()) throw new Exception("No valid bets declared.");
        
        decimal totalBet = 0;
        foreach(var b in bets) {
            if (b.Amount <= 0) throw new Exception("Invalid individual bet amount.");
            
            // Validate Types & Values
            string type = b.Type.ToLower();
            string val = b.Value.ToLower();

            switch (type) {
                case "number":
                    if (!int.TryParse(val, out int n) || n < 0 || n > 36)
                        throw new Exception($"Invalid roulette number: {val}");
                    if (b.Amount > 500) throw new Exception("Single number bet exceeds $500.00 limit.");
                    break;
                case "color":
                    if (val != "red" && val != "black")
                        throw new Exception($"Invalid color: {val}");
                    break;
                case "evenodd":
                    if (val != "even" && val != "odd")
                        throw new Exception($"Invalid even/odd value: {val}");
                    break;
                case "column":
                    if (!int.TryParse(val, out int col) || col < 1 || col > 3)
                        throw new Exception($"Invalid column: {val}");
                    break;
                case "dozen":
                    if (!int.TryParse(val, out int doz) || doz < 1 || doz > 3)
                        throw new Exception($"Invalid dozen: {val}");
                    break;
                default:
                    throw new Exception($"Invalid bet type: {b.Type}");
            }
            
            totalBet += b.Amount;
        }
        
        if (totalBet > 100000) throw new Exception("Total table bet exceeds $100,000.00 limit.");
        
        // Final integrity check
        if (Math.Abs(totalBet - amount) > 0.001m) throw new Exception("Bet Integrity Violation: Declared sum does not match transaction amount.");

        await base.PlaceBet(userId, sessionId, amount, betData);
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        using var lockHandle = await LockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));

        var round = await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");

            var lastBet = repo.GetLastBet(sessionId);
            decimal betAmount = lastBet?.Amount ?? 1.0m;
            
            var bets = new List<RouletteBetDto>();
            string mode = "Classic";
            try {
                if (!string.IsNullOrEmpty(lastBet?.BetData)) {
                    string jsonToParse = lastBet.BetData;
                    if (jsonToParse.Trim().StartsWith("\"")) {
                        jsonToParse = JsonSerializer.Deserialize<string>(jsonToParse) ?? "[]";
                    }

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    using var doc = JsonDocument.Parse(jsonToParse);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array) {
                        bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(jsonToParse, options) ?? new();
                    } else {
                        if (doc.RootElement.TryGetProperty("Bets", out var betsEl) || doc.RootElement.TryGetProperty("bets", out betsEl)) 
                            bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(betsEl.GetRawText(), options) ?? new();
                        if (doc.RootElement.TryGetProperty("Mode", out var modeEl) || doc.RootElement.TryGetProperty("mode", out modeEl)) 
                            mode = modeEl.GetString() ?? "Classic";
                    }
                }
            } catch { }
            
            var state = string.IsNullOrEmpty(session.GameState) 
                ? new RouletteState() 
                : JsonSerializer.Deserialize<RouletteState>(session.GameState) ?? new RouletteState();
            int nonce = state.Nonce++;
            session.GameState = JsonSerializer.Serialize(state);

            var directive = await BrainService.GetNextDirectiveAsync(session.UserId, GameId, betAmount, repo);
            
            int number = 0;
            var allNumbers = Enumerable.Range(0, 37).ToList();

            if (directive.DecisionType.Equals("Random", StringComparison.OrdinalIgnoreCase)) {
                number = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce, 0, 37);
            }
            else if (directive.TargetWinAmount > 0) {
                var winningCandidates = allNumbers.Where(n => CalculatePayout(n, bets, mode) > 0).ToList();
                if (winningCandidates.Any()) {
                    int idx = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce, 0, winningCandidates.Count);
                    number = winningCandidates[idx];
                } else {
                    number = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce, 0, 37); 
                }
            } 
            else {
                var losingCandidates = allNumbers.Where(n => CalculatePayout(n, bets, mode) == 0).ToList();
                if (losingCandidates.Any()) {
                    int idx = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce, 0, losingCandidates.Count);
                    number = losingCandidates[idx];
                } else {
                    number = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce, 0, 37); 
                }
            }
            
            // --- Multiplier Logic (Extreme Mode) ---
            var luckyNumbers = new Dictionary<int, int>();
            if (mode.Equals("Extreme", StringComparison.OrdinalIgnoreCase)) {
                // Use nonce-based seeds for randomness every spin
                int count = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + 500, 1, 6); // 1-5 numbers
                for (int i = 0; i < count; i++) {
                    int ln = RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + i + 1000, 0, 37);
                    if (!luckyNumbers.ContainsKey(ln)) {
                        int[] pool = { 50, 100, 100, 200, 250, 500 };
                        int mult = pool[RngService.GetNextInt(session.ServerSeed, session.ClientSeed, nonce + i + 2000, 0, pool.Length)];
                        luckyNumbers[ln] = mult;
                    }
                }
            }

            int winMultiplier = luckyNumbers.ContainsKey(number) ? luckyNumbers[number] : 0;
            decimal actualWin = CalculatePayout(number, bets, mode, winMultiplier);

            bool isRandom = directive.DecisionType.Equals("Random", StringComparison.OrdinalIgnoreCase);
            
            // Async Call
            if (actualWin > 0 && !await VaultService.CanAffordWinAsync(session.UserId, GameId, actualWin, repo, strictShadowCheck: !isRandom)) {
                var losingCandidates = allNumbers.Where(n => CalculatePayout(n, bets, mode) == 0).ToList();
                if (losingCandidates.Any()) number = losingCandidates[RngService.GetNextInt(session.ServerSeed, session.ClientSeed, 99, 0, losingCandidates.Count)];
                actualWin = CalculatePayout(number, bets, mode);
                winMultiplier = 0; // Lost multiplier if moved to losing num
            }

            var roundId = Guid.NewGuid();

            if (actualWin > 0) {
                repo.UpdateGamePoolBalance(GameId, -actualWin);
                await VaultService.ProcessWinAsync(session.UserId, actualWin, repo, roundId);
                await questService.UpdateProgressAsync(session.UserId, "WinAmount", actualWin, repo, RealTimeService, VaultService);
                
                session.TotalWon += actualWin;
            }
            
            await BrainService.UpdateProfileAsync(session.UserId, betAmount, actualWin, repo);

            int roundCount = repo.GetRoundCount(sessionId);
            RotateServerSeed(session, roundCount);

            var shadowDirective = await BrainService.DecideOutcomeAsync(session.UserId, session.GameId, betAmount, repo, isShadowMode: true);

            var round = new GameRound {
                Id = roundId,
                GameSessionId = sessionId,
                TotalBetAmount = betAmount,
                TotalWinAmount = actualWin,
                RoundNumber = roundCount + 1,
                ShadowBrainResult = JsonSerializer.Serialize(shadowDirective),
                RandomResult = JsonSerializer.Serialize(new { 
                    Number = number, 
                    Mode = mode, 
                    LuckyNumbers = luckyNumbers,
                    Multiplier = winMultiplier 
                }),
                DecisionType = directive.DecisionType,
                ExecutedAt = DateTime.UtcNow,
                ServerSeed = session.ServerSeed,
                ServerSeedHash = session.ServerSeedHash,
                ClientSeed = session.ClientSeed,
                Nonce = nonce
            };

            repo.SaveRound(round);
            repo.UpdateSession(session); // CRITICAL: Persist the incremented nonce/state
            
            var user = repo.GetUser(session.UserId);
            if (user != null && !user.Username.StartsWith("Sim_")) {
                await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Roulette", Number = number, Win = actualWin, LuckyNumbers = luckyNumbers, Multiplier = winMultiplier });
            }
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

    private decimal CalculatePayout(int number, List<RouletteBetDto> bets, string mode = "Classic", int activeMultiplier = 0) {
        decimal total = 0;
        decimal straightUpMult = mode.Equals("Extreme", StringComparison.OrdinalIgnoreCase) ? 30m : 36m;

        foreach (var bet in bets) {
            bool win = false;
            decimal mult = 0;

            if (bet.Type.Equals("number", StringComparison.OrdinalIgnoreCase) && int.TryParse(bet.Value, out int target) && target == number) {
                win = true; 
                mult = (activeMultiplier > 0) ? (decimal)activeMultiplier : straightUpMult;
            }
            else if (bet.Type.Equals("color", StringComparison.OrdinalIgnoreCase)) {
                bool isRed = new[] { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 }.Contains(number);
                if (number == 0) {
                    total += bet.Amount * 0.5m;
                }
                else if ((bet.Value.Equals("red", StringComparison.OrdinalIgnoreCase) && isRed) || (bet.Value.Equals("black", StringComparison.OrdinalIgnoreCase) && !isRed)) {
                    win = true; mult = 2m;
                }
            }
            else if (bet.Type.Equals("evenodd", StringComparison.OrdinalIgnoreCase)) {
                if (number == 0) {
                    total += bet.Amount * 0.5m;
                }
                else {
                    bool isEven = number % 2 == 0;
                    if ((bet.Value.Equals("even", StringComparison.OrdinalIgnoreCase) && isEven) || (bet.Value.Equals("odd", StringComparison.OrdinalIgnoreCase) && !isEven)) {
                        win = true; mult = 2m;
                    }
                }
            }

            if (win) total += bet.Amount * mult;
        }
        return total;
    }

    public override Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) => Task.CompletedTask;
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome { GameRoundId = roundId });
    
    // Implemented Recovery!
    public override async Task<object?> GetCurrentState(Guid sessionId) {
         return await ExecuteScopedAsync((repo, _, _) => {
            var round = repo.GetLastRound(sessionId);
            if (round == null) return Task.FromResult<object?>(null);
            // For Roulette, previous state is just the last result (number)
            return Task.FromResult<object?>(JsonSerializer.Deserialize<object>(round.RandomResult));
        });
    }
}
