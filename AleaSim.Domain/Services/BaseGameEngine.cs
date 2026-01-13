using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Domain.Services;

public abstract class BaseGameEngine : IGame {
    protected readonly IRngService RngService;
    protected readonly IVaultService VaultService;
    protected readonly IBrainService BrainService;
    protected readonly IPromotionService PromotionService;
    protected readonly IJackpotService JackpotService;
    protected readonly IRealTimeService RealTimeService;
    protected readonly IServiceScopeFactory ScopeFactory;

    protected BaseGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope) {
        RngService = rng;
        VaultService = vault;
        BrainService = brain;
        PromotionService = promo;
        JackpotService = jackpot;
        RealTimeService = realTime;
        ScopeFactory = scope;
    }

    public virtual async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string betData) {
        await ExecuteScopedAsync(async (repo, questService, levelService) => {
            var session = repo.GetSession(sessionId);
            if (session == null) throw new Exception("Session not found");
            
            // SECURITY CHECK: Session Hijacking Prevention
            if (session.UserId != userId) {
                // Log potentially malicious activity if possible
                throw new UnauthorizedAccessException("Session does not belong to the calling user.");
            }
            
            if (await VaultService.ProcessBetAsync(session.UserId, amount, repo)) {
                var bet = new Bet {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    UserId = session.UserId,
                    Amount = amount,
                    BetData = betData,
                    CreatedAt = DateTime.UtcNow
                };
                repo.SaveBet(bet);
                
                BrainService.UpdateProfile(session.UserId, amount, 0);
                PromotionService.ProcessBetActivity(session.UserId, amount, repo);
                await JackpotService.Contribute(session.GameId, amount, repo);
                
                // Quest Integration
                questService.GenerateDailyQuests(session.UserId, repo);
                await questService.UpdateProgressAsync(session.UserId, "SpinCount", 1, repo, VaultService);
                await levelService.AddExperience(session.UserId, amount, repo, RealTimeService);
            } else {
                throw new Exception("Insufficient funds");
            }
        });
    }

    public virtual async Task<GameSession> StartSession(Guid userId, int? seed = null, string? metadata = null) {
        return await ExecuteScopedAsync(async repo => {
            var session = new GameSession {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = Guid.Empty, // Overridden by sub-engine if needed
                Seed = System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue),
                StartedAt = DateTime.UtcNow,
                IsActive = true
            };
            return repo.CreateSession(session);
        });
    }

    public abstract Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard);
    public abstract Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData);
    public abstract Task<Outcome> GetOutcome(Guid roundId);
    public abstract Task<object?> GetCurrentState(Guid sessionId);

    protected async Task ExecuteScopedAsync(Func<IGameRepository, Task> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        using var tx = repo.BeginTransaction();
        try {
            await action(repo);
            tx.Commit();
        } catch {
            tx.Rollback();
            throw;
        }
    }

    protected async Task ExecuteScopedAsync(Func<IGameRepository, IQuestService, ILevelService, Task> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var questService = scope.ServiceProvider.GetRequiredService<IQuestService>();
        var levelService = scope.ServiceProvider.GetRequiredService<ILevelService>();
        using var tx = repo.BeginTransaction();
        try {
            await action(repo, questService, levelService);
            tx.Commit();
        } catch {
            tx.Rollback();
            throw;
        }
    }

    protected async Task<T> ExecuteScopedAsync<T>(Func<IGameRepository, Task<T>> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        using var tx = repo.BeginTransaction();
        try {
            var result = await action(repo);
            tx.Commit();
            return result;
        } catch {
            tx.Rollback();
            throw;
        }
    }
    
    protected async Task<T> ExecuteScopedAsync<T>(Func<IGameRepository, IQuestService, ILevelService, Task<T>> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var questService = scope.ServiceProvider.GetRequiredService<IQuestService>();
        var levelService = scope.ServiceProvider.GetRequiredService<ILevelService>();
        using var tx = repo.BeginTransaction();
        try {
            var result = await action(repo, questService, levelService);
            tx.Commit();
            return result;
        } catch {
            tx.Rollback();
            throw;
        }
    }
    
    protected T ExecuteScoped<T>(Func<IGameRepository, T> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        using var tx = repo.BeginTransaction();
        try {
            var result = action(repo);
            tx.Commit();
            return result;
        } catch {
            tx.Rollback();
            throw;
        }
    }
}
