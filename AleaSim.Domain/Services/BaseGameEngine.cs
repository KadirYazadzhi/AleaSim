using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Domain.Services;

public abstract class BaseGameEngine : IGame {
    protected readonly IRngService RngService;
    protected readonly IVaultService VaultService; // Replaces RtpEngine for money
    protected readonly IBrainService BrainService; // New Brain
    protected readonly IPromotionService PromotionService; // New Promotions
    protected readonly IJackpotService JackpotService;
    protected readonly IRealTimeService RealTimeService;
    protected readonly IServiceScopeFactory ScopeFactory;

    protected BaseGameEngine(IRngService rngService, IVaultService vaultService, IBrainService brainService, IPromotionService promotionService, IJackpotService jackpotService, IRealTimeService realTimeService, IServiceScopeFactory scopeFactory) {
        RngService = rngService;
        VaultService = vaultService;
        BrainService = brainService;
        PromotionService = promotionService;
        JackpotService = jackpotService;
        RealTimeService = realTimeService;
        ScopeFactory = scopeFactory;
    }

    // Helper to execute logic in a scope with repository
    protected async Task<T> ExecuteScopedAsync<T>(Func<IGameRepository, Task<T>> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        return await action(repo);
    }

    protected async Task ExecuteScopedAsync(Func<IGameRepository, Task> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        await action(repo);
    }

    // Synchronous versions for internal logic if needed
    protected T ExecuteScoped<T>(Func<IGameRepository, T> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        return action(repo);
    }

    protected void ExecuteScoped(Action<IGameRepository> action) {
        using var scope = ScopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        action(repo);
    }

    public virtual async Task<GameSession> StartSession(Guid userId, int? seed = null) {
        return await ExecuteScopedAsync(async repo => {
            int newSeed = seed ?? System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
            var session = new GameSession {
                Id = Guid.NewGuid(),
                UserId = userId,
                Seed = newSeed,
                StartedAt = DateTime.UtcNow,
                IsActive = true,
                GameId = GetGameId(repo)
            };
            repo.CreateSession(session);
            return await Task.FromResult(session);
        });
    }

    public virtual async Task PlaceBet(Guid sessionId, decimal amount, string betData) {
        if (amount <= 0) throw new ArgumentException("Bet amount must be positive.");

        await ExecuteScopedAsync(async repo => {
            using var transaction = repo.BeginTransaction();
            try {
                var session = repo.GetSession(sessionId);
                if (session == null || !session.IsActive) {
                    throw new InvalidOperationException("Session not found or inactive.");
                }

                // Use VaultService to process the bet (Handles Bonus vs Real balance)
                bool fundsDeducted = VaultService.ProcessBet(session.UserId, amount, repo);
                if (!fundsDeducted) {
                    throw new InvalidOperationException("Insufficient balance (Real or Bonus).");
                }

                var bet = new Bet {
                    Id = Guid.NewGuid(),
                    GameSessionId = sessionId,
                    UserId = session.UserId,
                    Amount = amount,
                    BetData = betData,
                    CreatedAt = DateTime.UtcNow
                };
                repo.SaveBet(bet);

                // Promotions Tracking (Tickets & Activity)
                PromotionService.ProcessBetActivity(session.UserId, amount, repo);

                // Stats & Jackpot Contribution
                await JackpotService.Contribute(session.GameId, amount, repo);
                
                // Update Brain Profile about the bet
                BrainService.UpdateProfile(session.UserId, amount, 0);

                transaction.Commit();
            }
            catch {
                transaction.Rollback();
                throw;
            }
        });
    }

    public abstract Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard);

    public abstract Task<Outcome> GetOutcome(Guid roundId);

    public virtual async Task ProcessAction(Guid sessionId, string action, string actionData) {
        await Task.CompletedTask;
    }

    public virtual async Task<object?> GetCurrentState(Guid sessionId) {
        return await Task.Run(() => ExecuteScoped<object?>(repo => {
             return null; 
        }));
    }

    protected void EndSession(Guid sessionId) {
        ExecuteScoped(repo => repo.EndSession(sessionId));
    }

    protected void UpdateBalance(Guid userId, decimal amount, IGameRepository repo) {
        repo.UpdateUserBalance(userId, amount);
    }
    
    protected Guid GetGameId(IGameRepository repo) {
        string gameName = this.GetType().Name.Replace("GameEngine", "");
        var game = repo.GetGameByType(gameName);
        
        if (game == null) {
            game = new Game { Id = Guid.NewGuid(), Name = gameName, Type = gameName };
            repo.CreateGame(game);
        }
        return game.Id;
    }
}