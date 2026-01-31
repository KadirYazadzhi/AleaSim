using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AleaSim.Domain.Services;

public class SimulationService : ISimulationService {
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<string, IGame> _gameFactory;

    public SimulationService(IServiceProvider serviceProvider, Func<string, IGame> gameFactory) {
        _serviceProvider = serviceProvider;
        _gameFactory = gameFactory;
    }

    public async Task<SimulationReport> RunSimulation(SimulationRequest request) {
        var stopwatch = Stopwatch.StartNew();
        var engine = _gameFactory(request.GameType);
        
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

        // 1. Create Dummy User with funds
        var dummyUser = new User {
            Id = Guid.NewGuid(),
            Username = $"Sim_{Guid.NewGuid().ToString().Substring(0, 8)}",
            Email = "sim@internal.local",
            Balance = 100000000m, // Infinite funds
            Role = AleaSim.Domain.Enums.Role.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        repo.CreateUser(dummyUser);
        
        repo.CreatePlayerProfile(new PlayerProfile { 
            UserId = dummyUser.Id, 
            TotalWagered = 0, 
            TotalPaid = 0,
            LastSpinTimestamp = DateTime.UtcNow 
        });

        try {
            var game = repo.GetGameByType(request.GameType);
            if (game == null) throw new Exception($"Game type '{request.GameType}' not found in DB.");

            decimal totalBet = 0;
            decimal totalWin = 0;
            decimal maxWin = 0;
            int bonusCount = 0;
            int respinCount = 0;
            var distribution = new Dictionary<string, int>();

            var session = await engine.StartSession(dummyUser.Id, game.Id);

            for (int i = 0; i < request.Iterations; i++) {
                await engine.PlaceBet(dummyUser.Id, session.Id, request.BetAmount, "{}");
                totalBet += request.BetAmount;

                var round = await engine.ResolveRound(session.Id);
                totalWin += round.TotalWinAmount;

                if (round.TotalWinAmount > maxWin) maxWin = round.TotalWinAmount;

                if (round.RandomResult.Contains("\"IsBonusActive\":true")) bonusCount++;
                if (round.RandomResult.Contains("\"IsRespinActive\":true")) respinCount++;

                string dType = round.DecisionType ?? "Unknown";
                if (!distribution.ContainsKey(dType)) distribution[dType] = 0;
                distribution[dType]++;
            }

            stopwatch.Stop();

            decimal rtpResult = totalBet > 0 ? (totalWin / totalBet) * 100m : 0m;

            return new SimulationReport {
                GameType = request.GameType,
                TotalIterations = request.Iterations,
                TotalBet = totalBet,
                TotalWin = totalWin,
                ActualRTP = (double)rtpResult,
                MaxWin = maxWin,
                BonusGamesTriggered = bonusCount,
                RespinsTriggered = respinCount,
                DecisionDistribution = distribution,
                ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
        finally {
            try {
                repo.DeleteUser(dummyUser.Id);
            } catch {} 
        }
    }
}