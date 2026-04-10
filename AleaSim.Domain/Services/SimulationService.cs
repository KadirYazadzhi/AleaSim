using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class SimulationService : ISimulationService {
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<string, IGame> _gameFactory;
    private readonly IAuditService _auditService;

    public SimulationService(IServiceProvider serviceProvider, Func<string, IGame> gameFactory, IAuditService auditService) {
        _serviceProvider = serviceProvider;
        _gameFactory = gameFactory;
        _auditService = auditService;
    }

    public async Task<SimulationReport> RunSimulation(SimulationRequest request) {
        var stopwatch = Stopwatch.StartNew();
        
        string factoryKey = request.GameType;
        if (factoryKey.Equals("dice_slider", StringComparison.OrdinalIgnoreCase) || 
            factoryKey.Equals("dice_multi", StringComparison.OrdinalIgnoreCase)) factoryKey = "dice";
            
        var engine = _gameFactory(factoryKey);
        
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

        // 1. Create Veteran Dummy User (to bypass new player bonuses)
        var dummyUser = new User {
            Id = Guid.NewGuid(),
            Username = $"Sim_{Guid.NewGuid().ToString().Substring(0, 8)}",
            Email = "sim@internal.local",
            Balance = 100000000m, // Infinite funds
            Role = AleaSim.Domain.Enums.Role.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddYears(-1)
        };
        repo.CreateUser(dummyUser);
        
        // "Veteran" profile
        repo.CreatePlayerProfile(new PlayerProfile { 
            UserId = dummyUser.Id, 
            TotalWagered = 1000000m, 
            TotalPaid = 950000m,
            LastSpinTimestamp = DateTime.UtcNow.AddMinutes(-5) 
        });

        try {
            var game = repo.GetGameByType(factoryKey);
            if (game == null) throw new Exception($"Game type '{factoryKey}' not found in DB.");

            if (request.BetAmount <= 0) request.BetAmount = 1.0m;

            decimal totalBet = 0;
            decimal totalWin = 0;
            decimal maxWin = 0;
            int bonusCount = 0;
            int respinCount = 0;
            var distribution = new Dictionary<string, int>();
            var detailedResults = new List<SimulationDetail>();

            var session = await engine.StartSession(dummyUser.Id, game.Id);
            if (!string.IsNullOrEmpty(request.ForcedSeed)) {
                session.ServerSeed = request.ForcedSeed; // Time Travel: Override server seed
                repo.UpdateSession(session);
            }
            
            bool pendingFeature = false;

            for (int i = 0; i < request.Iterations; i++) {
                // 1. Prepare Bet Data based on Game Type
                string betData = "{}";
                string effectiveGameType = request.GameType.ToLower();

                if (effectiveGameType == "roulette") {
                    var mode = request.GameMode ?? "Classic";
                    var bets = new[] { new { Type = "color", Value = "red", Amount = request.BetAmount } };
                    betData = JsonSerializer.Serialize(new { Bets = bets, Mode = mode });
                } else if (effectiveGameType == "slot") {
                    betData = "{\"Denomination\":0.01}";
                } else if (effectiveGameType == "fruitblast") {
                    betData = "{\"Denomination\":0.01}";
                } else if (effectiveGameType == "blackjack") {
                    betData = "{}"; // Standard bet
                } else if (effectiveGameType == "baccarat") {
                    betData = JsonSerializer.Serialize(new { Type = "Player" });
                } else if (effectiveGameType == "dice" || effectiveGameType == "dice_slider") {
                    betData = JsonSerializer.Serialize(new { 
                        Type = "Slider", 
                        TargetValue = 50.50m, 
                        Condition = "Over",
                        Mode = "Slider"
                    });
                } else if (effectiveGameType == "dice_multi") {
                    betData = JsonSerializer.Serialize(new { 
                        Mode = "Multi",
                        MultiDiceSelected = new List<int> { 6 } 
                    });
                }

                // 2. Place Bet (if not in a pending feature like Free Spins)
                // IMPORTANT: We must track the INTENDED bet for RTP calculation even in features
                // to see the true return of the math model.
                if (!pendingFeature) {
                    await engine.PlaceBet(dummyUser.Id, session.Id, request.BetAmount, betData);
                    // Track total theoretical bet (the denominator for RTP)
                    totalBet += request.BetAmount;
                }

                var round = await engine.ResolveRound(session.Id);
                
                // For Blackjack, handle basic strategy (Hit until 17)
                if (request.GameType.Equals("Blackjack", StringComparison.OrdinalIgnoreCase)) {
                    int safetyGuard = 0;
                    
                    while (safetyGuard < 15) {
                        safetyGuard++;
                        var stateObj = await engine.GetCurrentState(session.Id);
                        if (stateObj == null) break;
                        
                        string json = JsonSerializer.Serialize(stateObj);
                        if (json.Contains("\"IsRoundOver\":true")) break;

                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        var hands = root.GetProperty("PlayerHands");
                        var activeIdx = root.GetProperty("ActiveHandIndex").GetInt32();
                        var currentCards = hands[activeIdx].GetProperty("Cards");
                        
                        int handValue = 0;
                        int aces = 0;
                        foreach (var card in currentCards.EnumerateArray()) {
                            string c = card.GetString() ?? "";
                            string rank = c.Substring(0, c.Length - 1);
                            if (rank == "A") aces++;
                            else if (rank == "10" || rank == "J" || rank == "Q" || rank == "K") handValue += 10;
                            else handValue += int.Parse(rank);
                        }
                        for (int a = 0; a < aces; a++) {
                            if (handValue + 11 <= 21) handValue += 11;
                            else handValue += 1;
                        }

                        if (handValue < 17) {
                            await engine.ProcessAction(dummyUser.Id, session.Id, "Hit", "{}");
                        } else {
                            await engine.ProcessAction(dummyUser.Id, session.Id, "Stand", "{}");
                        }
                    }
                    
                    // Final fetch of the round to get the WIN (from a new scope to avoid EF stale data)
                    using var innerScope = _serviceProvider.CreateScope();
                    var innerRepo = innerScope.ServiceProvider.GetRequiredService<IGameRepository>();
                    round = innerRepo.GetLastRound(session.Id) ?? round;
                }
                // ... (multi-step logic)

                totalWin += round.TotalWinAmount;
                if (round.TotalWinAmount > maxWin) maxWin = round.TotalWinAmount;

                // Track detailed result for CSV (limit to 10k to save memory)
                if (i < 10000) {
                    detailedResults.Add(new SimulationDetail {
                        BetAmount = pendingFeature ? 0 : request.BetAmount,
                        WinAmount = round.TotalWinAmount,
                        DecisionType = round.DecisionType ?? "Random"
                    });
                }

                // ... (feature tracking)
                bool isBonus = round.RandomResult.Contains("\"IsBonusActive\":true");
                bool isRespin = round.RandomResult.Contains("\"IsRespinActive\":true");
                
                if (isBonus && !pendingFeature) bonusCount++;
                if (isRespin && !pendingFeature) respinCount++;
                
                pendingFeature = isBonus || isRespin;

                string dType = round.DecisionType ?? "Unknown";
                if (!distribution.ContainsKey(dType)) distribution[dType] = 0;
                distribution[dType]++;
            }

            stopwatch.Stop();

            decimal rtpResult = totalBet > 0 ? (totalWin / totalBet) * 100m : 0m;

            var report = new SimulationReport {
                GameType = request.GameType,
                TotalIterations = request.Iterations,
                TotalBet = totalBet,
                TotalWin = totalWin,
                ActualRTP = rtpResult,
                MaxWin = maxWin,
                BonusGamesTriggered = bonusCount,
                RespinsTriggered = respinCount,
                DecisionDistribution = distribution,
                ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                DetailedResults = detailedResults
            };

            // Persist Report
            _auditService.LogEvent("SIMULATION_REPORT", $"Simulated {request.Iterations} rounds of {request.GameType}. RTP: {rtpResult:F2}% (Bet: {totalBet:C}, Win: {totalWin:C})", dummyUser.Id.ToString(), JsonSerializer.Serialize(report));

            return report;
        }
        finally {
            try {
                // Use the enhanced deletion method in repo
                repo.DeleteUser(dummyUser.Id);
            } catch {} 
        }
    }
}