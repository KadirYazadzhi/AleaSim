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
        var engine = _gameFactory(request.GameType);
        
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
            var game = repo.GetGameByType(request.GameType);
            if (game == null) throw new Exception($"Game type '{request.GameType}' not found in DB.");

            if (request.BetAmount <= 0) request.BetAmount = 1.0m;

            decimal totalBet = 0;
            decimal totalWin = 0;
            decimal maxWin = 0;
            int bonusCount = 0;
            int respinCount = 0;
            var distribution = new Dictionary<string, int>();
            var detailedResults = new List<SimulationDetail>();

            var session = await engine.StartSession(dummyUser.Id, game.Id);
            bool pendingFeature = false;

            // PERFORMANCE FIX: Wrap simulation in a single transaction
            using var transaction = repo.BeginTransaction();

            for (int i = 0; i < request.Iterations; i++) {
                // 1. Prepare Bet Data based on Game Type
                string betData = "{}";
                if (request.GameType.Equals("Roulette", StringComparison.OrdinalIgnoreCase)) {
                    var mode = request.GameMode ?? "Classic";
                    var bets = new[] { new { Type = "color", Value = "red", Amount = request.BetAmount } };
                    betData = JsonSerializer.Serialize(new { Bets = bets, Mode = mode });
                } else if (request.GameType.Equals("Slot", StringComparison.OrdinalIgnoreCase)) {
                    betData = "{\"Denomination\":0.01}";
                } else if (request.GameType.Equals("Blackjack", StringComparison.OrdinalIgnoreCase)) {
                    betData = "{}"; // Standard bet
                } else if (request.GameType.Equals("Baccarat", StringComparison.OrdinalIgnoreCase)) {
                    betData = JsonSerializer.Serialize(new { Type = "Player" });
                } else if (request.GameType.Equals("Dice", StringComparison.OrdinalIgnoreCase)) {
                    betData = JsonSerializer.Serialize(new { 
                        Type = "Slider", 
                        Target = 50.50m, 
                        IsOver = true,
                        Mode = "Slider"
                    });
                }

                // 2. Place Bet (if not in a pending feature like Free Spins)
                if (!pendingFeature) {
                    await engine.PlaceBet(dummyUser.Id, session.Id, request.BetAmount, betData);
                    totalBet += request.BetAmount;
                }

                var round = await engine.ResolveRound(session.Id);
                
                // For Blackjack, we need to handle actions if the round is not over
                if (request.GameType.Equals("Blackjack", StringComparison.OrdinalIgnoreCase)) {
                    // Simple simulation logic: Always Stand immediately for simplicity in mass simulation
                    // or implement basic strategy. For now, Stand to resolve.
                    if (round.RandomResult.Contains("\"IsRoundOver\":false")) {
                         await engine.ProcessAction(dummyUser.Id, session.Id, "Stand", "{}");
                         round = await engine.ResolveRound(session.Id); // Get final state
                    }
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

            transaction.Commit();
            stopwatch.Stop();

            decimal rtpResult = totalBet > 0 ? (totalWin / totalBet) * 100m : 0m;

            var report = new SimulationReport {
                GameType = request.GameType,
                TotalIterations = request.Iterations,
                TotalBet = totalBet,
                TotalWin = totalWin,
                ActualRTP = (double)rtpResult,
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