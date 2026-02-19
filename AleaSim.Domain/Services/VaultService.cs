using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;

namespace AleaSim.Domain.Services;

public class VaultService : IVaultService {
    private readonly IRealTimeService _realTime;
    private readonly ILockService _lockService;
    private readonly IRedisCacheService _cache;

    public VaultService(IRealTimeService realTime, ILockService lockService, IRedisCacheService cache) {
        _realTime = realTime;
        _lockService = lockService;
        _cache = cache;
    }

    public async Task<bool> ProcessBetAsync(Guid userId, decimal amount, IGameRepository repo) {
        if (amount < 0) return false;
        
        // Lock specifically for this user's wallet
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));

        var user = repo.GetUser(userId);
        var profile = repo.GetPlayerProfile(userId);
        if (user == null) return false;

        bool success = false;

        // Admin role used to have free play, but we've enabled real deductions for better testing/realism.
        // Admins can always top-up via the Backoffice UI.
        
        if (user.BonusBalance > 0) {
            if (user.BonusBalance >= amount) {
                user.BonusBalance -= amount;
                if (user.WageringRequirement > 0) {
                    user.WageringProgress += amount;
                    CheckWageringCompletion(user);
                }
                success = true;
            }
            else {
                decimal remainder = amount - user.BonusBalance;
                decimal bonusPart = user.BonusBalance;
                user.BonusBalance = 0;
                if (user.WageringRequirement > 0) {
                        user.WageringProgress += bonusPart;
                        CheckWageringCompletion(user);
                }
                if (user.Balance >= remainder) {
                    user.Balance -= remainder;
                    success = true;
                }
            }
        }
        else if (user.Balance >= amount) {
            user.Balance -= amount;
            success = true;
        }

        if (success) {
            if (profile != null) {
                if (user.WageringRequirement == 0) {
                     profile.ShadowBalance += amount * 0.95m;
                     repo.UpdatePlayerProfile(profile);
                }
            }
            
            repo.UpdateUser(user);
            
            // CACHE INVALIDATION: Update balance in Redis
            _ = _cache.RemoveAsync($"user:profile:{userId}");
            
            repo.SaveTransaction(new Transaction {
                Id = Guid.NewGuid(), 
                UserId = userId, 
                Amount = -amount, 
                Type = TransactionType.Bet, 
                Description = (user.Role == Role.Admin) ? "Admin Bet" : "Game Bet", 
                Timestamp = DateTime.UtcNow, 
                ResultingBalance = user.Balance
            });

            _ = _realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance);
        }
        return success;
    }

    public async Task ProcessWinAsync(Guid userId, decimal amount, IGameRepository repo) {
        if (amount <= 0) return;

        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));

        var user = repo.GetUser(userId);
        var profile = repo.GetPlayerProfile(userId);
        if (user == null) return;

        if (profile != null && user.WageringRequirement == 0) {
            profile.ShadowBalance -= amount;
            repo.UpdatePlayerProfile(profile);
        }

        if (user.BonusBalance > 0 && user.WageringProgress < user.WageringRequirement) {
            user.BonusBalance += amount;
        } else {
            user.Balance += amount;
        }

        repo.UpdateUser(user);
        
        // CACHE INVALIDATION: Update balance in Redis
        _ = _cache.RemoveAsync($"user:profile:{userId}");

        repo.SaveTransaction(new Transaction {
            Id = Guid.NewGuid(), UserId = userId, Amount = amount, Type = TransactionType.Win, 
            Description = "Game Win", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
        });

        _ = _realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance);
    }

    public async Task<bool> CanAffordWinAsync(Guid userId, Guid gameId, decimal winAmount, IGameRepository repo, bool strictShadowCheck = true) {
        // Just reading values, theoretically requires lock if strict consistency is needed, 
        // but for performance we might skip lock here since we don't write.
        // However, to be safe:
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(2));
        
        var game = repo.GetGame(gameId);
        var profile = repo.GetPlayerProfile(userId);
        if (game == null) return false;
        
        bool casinoCanAfford = game.PoolBalance >= winAmount;
        bool userHasShadowCredit = profile == null || profile.ShadowBalance >= winAmount;
        
        if (!strictShadowCheck) return casinoCanAfford;
        return casinoCanAfford && userHasShadowCredit;
    }

    public bool CanAffordWinCheck(Guid userId, Guid gameId, decimal winAmount, IGameRepository repo, bool strictShadowCheck = true) {
        var game = repo.GetGame(gameId);
        var profile = repo.GetPlayerProfile(userId);
        if (game == null) return false;
        
        bool casinoCanAfford = game.PoolBalance >= winAmount;
        bool userHasShadowCredit = profile == null || profile.ShadowBalance >= winAmount;
        
        if (!strictShadowCheck) return casinoCanAfford;
        return casinoCanAfford && userHasShadowCredit;
    }

    public async Task CreditBonusAsync(Guid userId, decimal amount, decimal wageringRequirement, IGameRepository repo) {
        if (amount <= 0) return;
        
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));

        var user = repo.GetUser(userId);
        if (user == null) return;
        user.BonusBalance += amount;
        user.WageringRequirement += wageringRequirement;
        user.BonusLastUpdated = DateTime.UtcNow;
        repo.UpdateUser(user);

        repo.SaveTransaction(new Transaction {
            Id = Guid.NewGuid(), UserId = userId, Amount = amount, Type = TransactionType.Bonus, 
            Description = "Bonus Credited", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
        });

        _ = _realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance);
    }

    public async Task<bool> CashoutBonusAsync(Guid userId, IGameRepository repo) {
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));

        var user = repo.GetUser(userId);
        if (user == null || user.BonusBalance <= 0) return false;
        decimal amountToCredit = (user.BonusBalance >= 100) ? user.BonusBalance * 0.10m : 0;
        user.Balance += amountToCredit;
        user.BonusBalance = 0;
        user.WageringRequirement = 0;
        user.WageringProgress = 0;
        repo.UpdateUser(user);

        if (amountToCredit > 0) {
            repo.SaveTransaction(new Transaction {
                Id = Guid.NewGuid(), UserId = userId, Amount = amountToCredit, Type = TransactionType.Bonus, 
                Description = "Bonus Cashout", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
            });
        }

        _ = _realTime.NotifyBalanceUpdate(userId, user.Balance, 0);
        return true;
    }

    private void CheckWageringCompletion(User user) {
        if (user.WageringProgress >= user.WageringRequirement) {
            user.Balance += user.BonusBalance;
            user.BonusBalance = 0;
            user.WageringRequirement = 0;
            user.WageringProgress = 0;
        }
    }
}