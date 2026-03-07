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

        // 1. RESPONSIBLE GAMING: Self-Exclusion Check
        if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow) {
            throw new InvalidOperationException($"Your account is self-excluded until {user.LockoutUntil.Value:yyyy-MM-dd HH:mm} UTC.");
        }

        // 2. RESPONSIBLE GAMING: Daily Loss Limit Check
        if (user.DailyLossLimit.HasValue && user.DailyLossLimit.Value > 0) {
            var currentDailyLoss = repo.GetUserDailyLoss(userId, DateTime.UtcNow);
            if (currentDailyLoss + amount > user.DailyLossLimit.Value) {
                throw new InvalidOperationException($"Bet denied. This would exceed your daily loss limit of {user.DailyLossLimit.Value:C2}. Current loss: {currentDailyLoss:C2}.");
            }
        }

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
                     
                     // 10% Base Cashback + Level Bonus
                     decimal rate = 0.10m + (profile.CashbackLevel * 0.01m);
                     profile.PendingCashback += amount * rate;
                     
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

        // Note: Deduction from PoolBalance should ideally happen in the Engine or we need GameId here.
        // For now, we update the user balance.

        if (profile != null && user.WageringRequirement == 0) {
            profile.ShadowBalance -= amount;
            
            decimal rate = 0.10m + (profile.CashbackLevel * 0.01m);
            profile.PendingCashback = Math.Max(0, profile.PendingCashback - (amount * rate));

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

    public async Task<bool> CanAffordWinAsync(Guid userId, Guid gameId, decimal winAmount, IGameRepository repo, bool strictShadowCheck = false) {
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(2));
        
        var game = repo.GetGame(gameId);
        var profile = repo.GetPlayerProfile(userId);
        if (game == null) return false;
        
        bool casinoCanAfford = game.PoolBalance >= winAmount;
        
        // If not strict, we only care about the casino funds
        if (!strictShadowCheck) return casinoCanAfford;

        // Strict mode (Shadow Testing)
        bool userHasShadowCredit = profile == null || profile.ShadowBalance >= winAmount;
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

    public decimal GetPendingCashback(Guid userId, IGameRepository repo) {
        var profile = repo.GetPlayerProfile(userId);
        return profile?.PendingCashback ?? 0;
    }

    public async Task<decimal> ClaimCashbackAsync(Guid userId, IGameRepository repo) {
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));
        
        var profile = repo.GetPlayerProfile(userId);
        var user = repo.GetUser(userId);
        
        if (profile == null || user == null || profile.PendingCashback < 0.01m) return 0;

        decimal amount = Math.Round(profile.PendingCashback, 2);
        profile.PendingCashback = 0;
        user.Balance += amount;
        
        repo.UpdatePlayerProfile(profile);
        repo.UpdateUser(user);

        repo.SaveTransaction(new Transaction {
            Id = Guid.NewGuid(), UserId = userId, Amount = amount, Type = TransactionType.Bonus, 
            Description = "Cashback Claim", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
        });

        _ = _realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance);
        return amount;
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