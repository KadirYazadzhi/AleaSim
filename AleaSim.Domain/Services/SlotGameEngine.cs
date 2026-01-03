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
    private Guid GameId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private const int SYM_SEVEN = 7;
    private readonly int[] _baseSymbols = { 1, 2, 3, 4, 5, 6, 7, 8 };

    public SlotGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope) 
        : base(rng, vault, brain, promo, jackpot, realTime, scope) {
    }

    public override async Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard) {
        return await ExecuteScopedAsync(async (repo, questService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");

            var lastBet = repo.GetLastBet(sessionId);
            decimal betAmount = lastBet?.Amount ?? 1.0m;

            var decision = BrainService.DecideOutcome(session.UserId, GameId, betAmount, repo);
            var shadowDecision = BrainService.DecideOutcome(session.UserId, GameId, betAmount, repo, true);

            decimal winAmount = decision.TargetWinAmount;
            VaultService.ProcessWin(session.UserId, winAmount, repo);
            BrainService.UpdateProfile(session.UserId, betAmount, winAmount);
            
            if (winAmount > 0) {
                questService.UpdateProgress(session.UserId, "WinAmount", (int)winAmount, repo, VaultService);
            }

            // 4. Global Big Win Notification
            if (betAmount > 0 && (winAmount / betAmount) >= 100) {
                var user = repo.GetUser(session.UserId);
                var gameInfo = repo.GetGame(GameId);
                _ = RealTimeService.NotifyBigWin(user?.Username ?? "Lucky Player", gameInfo?.Name ?? "Clover Chase", winAmount, winAmount / betAmount);
            }

            var round = new GameRound {
                Id = Guid.NewGuid(),
                GameSessionId = sessionId,
                TotalBetAmount = betAmount,
                TotalWinAmount = winAmount,
                RandomResult = "{}",
                DecisionType = decision.DecisionType,
                ShadowBrainResult = JsonSerializer.Serialize(shadowDecision),
                ExecutedAt = DateTime.UtcNow
            };

            repo.SaveRound(round);
            await RealTimeService.NotifyGameUpdate(session.UserId, new { Win = winAmount });
            return round;
        });
    }

    public override Task ProcessAction(Guid sessionId, string action, string actionData) => Task.CompletedTask;
    public override async Task<Outcome> GetOutcome(Guid roundId) => new Outcome { GameRoundId = roundId };
    public override async Task<object?> GetCurrentState(Guid sessionId) => null;
}
