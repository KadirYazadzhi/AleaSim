using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
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
        
        decimal totalBet = 0;
        decimal totalWin = 0;
        decimal maxWin = 0;
        int bonusCount = 0;
        int respinCount = 0;
        var distribution = new Dictionary<string, int>();

        Guid dummyUserId = request.UserId ?? Guid.NewGuid();
        var session = await engine.StartSession(dummyUserId);

        for (int i = 0; i < request.Iterations; i++) {
            await engine.PlaceBet(session.Id, request.BetAmount, "{}");
            totalBet += request.BetAmount;

            var round = await engine.ResolveRound(session.Id);
            totalWin += round.TotalWinAmount;

            if (round.TotalWinAmount > maxWin) maxWin = round.TotalWinAmount;

            // Simplified checks
            if (round.RandomResult.Contains("IsBonusActive")) bonusCount++;
            if (round.RandomResult.Contains("IsRespinActive")) respinCount++;

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
}