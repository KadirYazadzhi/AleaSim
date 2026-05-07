using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace AleaSim.Domain.Services;

public abstract class BaseGameEngine : IGame {
    protected readonly IRngService RngService;
    protected readonly IVaultService VaultService;
    protected readonly IBrainService BrainService;
    protected readonly IPromotionService PromotionService;
    protected readonly IJackpotService JackpotService;
    protected readonly IRealTimeService RealTimeService;
    protected readonly IServiceScopeFactory ScopeFactory;
    protected readonly ILockService LockService;

    protected BaseGameEngine(IRngService rng, IVaultService vault, IBrainService brain, IPromotionService promo, IJackpotService jackpot, IRealTimeService realTime, IServiceScopeFactory scope, ILockService lockService) {
        RngService = rng;
        VaultService = vault;
        BrainService = brain;
        PromotionService = promo;
        JackpotService = jackpot;
        RealTimeService = realTime;
        ScopeFactory = scope;
        LockService = lockService;
    }

    public virtual async Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string? betData) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var questService = scope.ServiceProvider.GetRequiredService<IQuestService>();
        var levelService = scope.ServiceProvider.GetRequiredService<ILevelService>();

        var stopSetting = await repo.GetGlobalSettingAsync("EmergencyStop");
        if (stopSetting == "true") {
            throw new InvalidOperationException("SYSTEM_HALTED: The gaming platform is currently under maintenance.");
        }

        bool betProcessed = false;
        bool isExcluded = false;
        Guid currentUserId = Guid.Empty;

        // SECURITY: Acquire wallet lock BEFORE starting transaction and release AFTER commit
        // This prevents the "phantom balance" issue where lock is released but TX is not yet visible to other threads.
        using (await LockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(10))) {
            using (var tx = repo.BeginTransaction()) {
                try {
                    var session = await repo.GetSessionAsync(sessionId);
                    if (session == null) throw new Exception("Session not found");
                    if (session.UserId != userId) throw new UnauthorizedAccessException("Session belongs to another user.");
                    currentUserId = session.UserId;

                    var betId = Guid.NewGuid();
                    if (await VaultService.ProcessBetAsync(session.UserId, amount, repo, betId)) {
                        var bet = new Bet {
                            Id = betId,
                            GameSessionId = sessionId,
                            UserId = session.UserId,
                            Amount = amount,
                            BetData = betData ?? "{}",
                            CreatedAt = DateTime.UtcNow
                        };
                        repo.SaveBet(bet);
                        repo.UpdateGamePoolBalance(session.GameId, amount);
                        
                        // Update session stats for efficient Admin Dashboard (Issue 38)
                        session.TotalWagered += amount;
                        repo.UpdateSession(session);
                        
                        var user = repo.GetUser(session.UserId);
                        isExcluded = user?.Username.StartsWith("Sim_") == true; // Only exclude simulation users from financial reporting/jackpots
                        bool isAdmin = user?.Role == Role.Admin;

                        await BrainService.UpdateProfileAsync(session.UserId, amount, 0, repo);
                        tx.Commit();
                        betProcessed = true;
                    } else {
                        throw new Exception("Insufficient funds");
                    }
                } catch {
                    tx.Rollback();
                    throw;
                }
            }
        }

            // 4. Perform non-critical updates AFTER the primary financial transaction is committed
            if (betProcessed && currentUserId != Guid.Empty) {
                // Sync Cache immediately for UI responsiveness
                await BrainService.SyncProfileToCacheAsync(currentUserId, repo).ConfigureAwait(false);

                if (!isExcluded) {
                    var session = await repo.GetSessionAsync(sessionId); // Refresh session for safety
                    if (session != null) {
                        // These are performed outside the main transaction to prevent pool exhaustion
                        // but still sequential to avoid DB concurrency issues on the same repo context
                        await PromotionService.ProcessBetActivity(currentUserId, amount, repo);
                        
                        // Jackpots should still exclude Admins to avoid polluting real player pools
                        var user = repo.GetUser(currentUserId);
                        if (user?.Role != Role.Admin) {
                            await JackpotService.Contribute(session.GameId, amount, repo);
                        }
                        
                        await questService.GenerateDailyQuests(currentUserId, repo);
                        await questService.UpdateProgressAsync(currentUserId, "SpinCount", 1, repo, RealTimeService, VaultService);
                        await levelService.AddExperience(currentUserId, amount, repo, RealTimeService);
                    }
                }
            }
    }

    public virtual async Task<GameSession> StartSession(Guid userId, Guid gameId, int? seed = null, string? clientSeed = null) {
        return await ExecuteScopedAsync(repo => {
            var serverSeed = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(serverSeed));
            var serverSeedHash = Convert.ToHexString(hashBytes);

            var session = new GameSession {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = gameId,
                Seed = System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue),
                ServerSeed = serverSeed,
                ServerSeedHash = serverSeedHash,
                ClientSeed = clientSeed ?? Guid.NewGuid().ToString("N").Substring(0, 8),
                StartedAt = DateTime.UtcNow,
                IsActive = true
            };
            return Task.FromResult(repo.CreateSession(session));
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
            await action(repo).ConfigureAwait(false);
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
            await action(repo, questService, levelService).ConfigureAwait(false);
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
            var result = await action(repo).ConfigureAwait(false);
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
            var result = await action(repo, questService, levelService).ConfigureAwait(false);
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

    protected void RotateServerSeed(GameSession session, int roundCount) {
        // Rotate server seed every round to prevent long-term pattern analysis
        if (roundCount > 0) {
            var newServerSeed = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(newServerSeed));
            var newServerSeedHash = Convert.ToHexString(hashBytes);

            session.ServerSeed = newServerSeed;
            session.ServerSeedHash = newServerSeedHash;
            // Note: In a real app, we might want to preserve the old seed for verification of past rounds
            // but for simplicity here we rotate and future rounds use the new one.
        }
    }
}
