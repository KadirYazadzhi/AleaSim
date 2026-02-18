using AleaSim.Shared.Models;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class RouletteGameEngine : BaseGameEngine {
    private readonly ILockService _lockService;
    public class RouletteState { public int Nonce { get; set; } }
    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public RouletteGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, ILockService lockService) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {
        _lockService = lockService;
    }

    public override async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string betData) {
        var bets = new List<RouletteBetDto>();
        try {
            if (!string.IsNullOrEmpty(betData)) {
                string jsonToParse = betData;
                // Handle cases where the string might be double-quoted/escaped as a string literal
                if (betData.Trim().StartsWith("\"")) {
                    jsonToParse = JsonSerializer.Deserialize<string>(betData) ?? "[]";
                }

                using var doc = JsonDocument.Parse(jsonToParse);
                if (doc.RootElement.ValueKind == JsonValueKind.Array) {
                    bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(jsonToParse) ?? new();
                } else if (doc.RootElement.TryGetProperty("Bets", out var betsEl)) {
                    bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(betsEl.GetRawText()) ?? new();
                }
            }
        } catch { throw new Exception("Invalid bet data format."); }

        decimal totalBet = bets.Sum(x => x.Amount);
        
        // 2. Validate Limits
        if (totalBet > 100000) throw new Exception("Total table bet exceeds $100,000.00 limit.");
        if (bets.Any(x => x.Type == "number" && x.Amount > 100)) throw new Exception("Single number bet exceeds $100.00 limit.");
        if (Math.Round(totalBet, 2) != Math.Round(amount, 2)) throw new Exception("Bet Integrity Error: Declared bets do not match total amount.");

        // 3. Deduct money only if valid
        await base.PlaceBet(userId, sessionId, amount, betData);
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        using var lockHandle = await _lockService.AcquireLockAsync(sessionId.ToString(), TimeSpan.FromSeconds(5));

        return await ExecuteScopedAsync(async (repo, questService, levelService) => {
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

                    using var doc = JsonDocument.Parse(jsonToParse);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array) {
                        bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(jsonToParse) ?? new();
                    } else {
                        if (doc.RootElement.TryGetProperty("Bets", out var betsEl)) 
                            bets = JsonSerializer.Deserialize<List<RouletteBetDto>>(betsEl.GetRawText()) ?? new();
                        if (doc.RootElement.TryGetProperty("Mode", out var modeEl)) 
                            mode = modeEl.GetString() ?? "Classic";
                    }
                }
            } catch { }
            
            var decision = BrainService.DecideOutcome(session.UserId, GameId, betAmount, repo);
            
            int number = 0;
            var allNumbers = Enumerable.Range(0, 37).ToList();

            if (decision.DecisionType == "Random") {
                number = RngService.GetNextInt(session.Seed, 0, 0, 37);
            }
            else if (decision.TargetWinAmount > 0) {
                var winningCandidates = allNumbers.Where(n => CalculatePayout(n, bets, mode) > 0).ToList();
                if (winningCandidates.Any()) {
                    int idx = RngService.GetNextInt(session.Seed, 0, 0, winningCandidates.Count);
                    number = winningCandidates[idx];
                } else {
                    number = RngService.GetNextInt(session.Seed, 0, 0, 37); 
                }
            } 
            else {
                var losingCandidates = allNumbers.Where(n => CalculatePayout(n, bets, mode) == 0).ToList();
                if (losingCandidates.Any()) {
                    int idx = RngService.GetNextInt(session.Seed, 0, 0, losingCandidates.Count);
                    number = losingCandidates[idx];
                } else {
                    number = RngService.GetNextInt(session.Seed, 0, 0, 37); 
                }
            }
            
            // --- Multiplier Logic (Extreme Mode) ---
            var luckyNumbers = new Dictionary<int, int>();
            if (mode == "Extreme") {
                int count = RngService.GetNextInt(session.Seed, 777, 1, 6); // 1-5 numbers
                for (int i = 0; i < count; i++) {
                    int ln = RngService.GetNextInt(session.Seed, i + 100, 0, 37);
                    if (!luckyNumbers.ContainsKey(ln)) {
                        int[] pool = { 50, 100, 100, 200, 500 };
                        int mult = pool[RngService.GetNextInt(session.Seed, i + 200, 0, pool.Length)];
                        luckyNumbers[ln] = mult;
                    }
                }
            }

            int winMultiplier = luckyNumbers.ContainsKey(number) ? luckyNumbers[number] : 0;
            decimal actualWin = CalculatePayout(number, bets, mode, winMultiplier);

            bool isRandom = decision.DecisionType == "Random";
            
            // Async Call
            if (actualWin > 0 && !await VaultService.CanAffordWinAsync(session.UserId, GameId, actualWin, repo, strictShadowCheck: !isRandom)) {
                var losingCandidates = allNumbers.Where(n => CalculatePayout(n, bets, mode) == 0).ToList();
                if (losingCandidates.Any()) number = losingCandidates[RngService.GetNextInt(session.Seed, 99, 0, losingCandidates.Count)];
                actualWin = CalculatePayout(number, bets, mode);
                winMultiplier = 0; // Lost multiplier if moved to losing num
            }

            if (actualWin > 0) {
                await VaultService.ProcessWinAsync(session.UserId, actualWin, repo);
                await questService.UpdateProgressAsync(session.UserId, "WinAmount", (int)actualWin, repo, VaultService);
            }
            
            BrainService.UpdateProfile(session.UserId, betAmount, actualWin, repo);

            int roundCount = repo.GetRoundCount(sessionId);

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                TotalBetAmount = betAmount,
                TotalWinAmount = actualWin,
                RoundNumber = roundCount + 1,
                RandomResult = JsonSerializer.Serialize(new { 
                    Number = number, 
                    Mode = mode, 
                    LuckyNumbers = luckyNumbers,
                    Multiplier = winMultiplier 
                }),
                DecisionType = decision.DecisionType,
                ExecutedAt = DateTime.UtcNow
            };

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Roulette", Number = number, Win = actualWin, LuckyNumbers = luckyNumbers, Multiplier = winMultiplier });
            return round;
        });
    }

    private decimal CalculatePayout(int number, List<RouletteBetDto> bets, string mode = "Classic", int activeMultiplier = 0) {
        decimal total = 0;
        decimal straightUpMult = (mode == "Extreme") ? 30m : 36m;

        foreach (var bet in bets) {
            bool win = false;
            decimal mult = 0;

            if (bet.Type == "number" && int.TryParse(bet.Value, out int target) && target == number) {
                win = true; 
                mult = (activeMultiplier > 0) ? (decimal)activeMultiplier : straightUpMult;
            }
            else if (bet.Type == "color") {
                bool isRed = new[] { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 }.Contains(number);
                if (number == 0) {
                    total += bet.Amount * 0.5m;
                }
                else if ((bet.Value == "red" && isRed) || (bet.Value == "black" && !isRed)) {
                    win = true; mult = 2m;
                }
            }
            else if (bet.Type == "evenodd") {
                if (number == 0) {
                    total += bet.Amount * 0.5m;
                }
                else {
                    bool isEven = number % 2 == 0;
                    if ((bet.Value == "even" && isEven) || (bet.Value == "odd" && !isEven)) {
                        win = true; mult = 2m;
                    }
                }
            }

            if (win) total += bet.Amount * mult;
        }
        return total;
    }

    public override Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData) => Task.CompletedTask;
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome());
    
    // Implemented Recovery!
    public override async Task<object?> GetCurrentState(Guid sessionId) {
         return await ExecuteScopedAsync(async (repo, _, _) => {
            var round = repo.GetLastRound(sessionId);
            if (round == null) return null;
            // For Roulette, previous state is just the last result (number)
            return JsonSerializer.Deserialize<object>(round.RandomResult);
        });
    }
}
