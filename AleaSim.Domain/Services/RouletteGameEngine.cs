using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class RouletteGameEngine : BaseGameEngine {
    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public RouletteGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async (repo, questService) => {
            var session = repo.GetSession(sessionId);
            var lastBet = repo.GetLastBet(sessionId);
            decimal betAmount = lastBet?.Amount ?? 1.0m;
            int nonce = repo.GetRoundCount(sessionId) + 1;

            var decision = BrainService.DecideOutcome(session.UserId, GameId, betAmount, repo);
            int number = decision.TargetWinAmount > 0 ? 17 : RngService.GetNextInt(session.Seed, nonce, 0, 37);

            decimal winAmount = decision.TargetWinAmount;
            VaultService.ProcessWin(session.UserId, winAmount, repo);
            BrainService.UpdateProfile(session.UserId, betAmount, winAmount);
            
            if (winAmount > 0) {
                questService.UpdateProgress(session.UserId, "WinAmount", (int)winAmount, repo, VaultService);
            }

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                TotalBetAmount = betAmount,
                TotalWinAmount = winAmount,
                RandomResult = JsonSerializer.Serialize(new { Number = number }),
                DecisionType = decision.DecisionType,
                ExecutedAt = DateTime.UtcNow
            };

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Game = "Roulette", Number = number, Win = winAmount });
            return round;
        });
    }

    public override Task ProcessAction(Guid sessionId, string action, string actionData) => Task.CompletedTask;
    public override Task<Outcome> GetOutcome(Guid roundId) => Task.FromResult(new Outcome());
    public override Task<object?> GetCurrentState(Guid sessionId) => Task.FromResult<object?>(null);
}
