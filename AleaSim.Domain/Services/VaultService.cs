using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Helpers;
using AleaSim.Domain.Constants;
using Microsoft.Extensions.Logging;

namespace AleaSim.Domain.Services;

public class VaultService : IVaultService {
    private readonly IRealTimeService _realTime;
    private readonly ILockService _lockService;
    private readonly IRedisCacheService _cache;
    private readonly ILogger<VaultService> _logger;

    public VaultService(IRealTimeService realTime, ILockService lockService, IRedisCacheService cache, ILogger<VaultService> logger) {
        _realTime = realTime;
        _lockService = lockService;
        _cache = cache;
        _logger = logger;
    }
    
    // IMPROVEMENT: Safe fire-and-forget wrapper
    private void SafeFireAndForget(Task task, string operationName) {
        _ = Task.Run(async () => {
            try {
                await task;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Fire-and-forget task failed: {Operation}", operationName);
            }
        });
    }

    public async Task<bool> ProcessBetAsync(Guid userId, decimal amount, IGameRepository repo) {
        // SECURITY: Validate decimal input
        if (!ValidationHelper.IsValidDecimal(amount) || amount <= 0) {
            throw new ArgumentException("Bet amount must be a valid positive number.");
        }
        
        // SECURITY: Clamp to reasonable maximum (prevent overflow)
        amount = ValidationHelper.ValidateAndClamp(amount, GameConstants.MIN_BET, GameConstants.MAX_BET);
        
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));
        using var transaction = repo.BeginTransaction();

        try {
            var user = repo.GetUser(userId);
            var profile = repo.GetPlayerProfile(userId);
            if (user == null) {
                transaction.Rollback();
                return false;
            }

            // 1. RESPONSIBLE GAMING: Self-Exclusion Check
            if (user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow) {
                transaction.Rollback();
                throw new InvalidOperationException($"Your account is self-excluded until {user.LockoutUntil.Value:yyyy-MM-dd HH:mm} UTC.");
            }

            // 2. RESPONSIBLE GAMING: Daily Loss Limit Check
            if (user.DailyLossLimit.HasValue && user.DailyLossLimit.Value > 0) {
                var currentDailyLoss = repo.GetUserDailyLoss(userId, DateTime.UtcNow);
                if (currentDailyLoss + amount > user.DailyLossLimit.Value) {
                    transaction.Rollback();
                    throw new InvalidOperationException($"Bet denied. This would exceed your daily loss limit of {user.DailyLossLimit.Value:C2}. Current loss: {currentDailyLoss:C2}.");
                }
            }

            bool success = false;

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

            if (success && profile != null) {
                if (user.WageringRequirement == 0) {
                     // SECURITY: Round calculations to prevent rounding error exploits
                     profile.ShadowBalance += Math.Round(amount * GameConstants.SHADOW_BALANCE_RATE, 2, MidpointRounding.ToZero);
                     
                     // SECURITY: Cap cashback level at maximum
                     int effectiveLevel = Math.Min(profile.CashbackLevel, GameConstants.MAX_CASHBACK_LEVEL);
                     decimal rate = GameConstants.BASE_CASHBACK_RATE + (effectiveLevel * GameConstants.CASHBACK_PER_LEVEL);
                     profile.PendingCashback += Math.Round(amount * rate, 2, MidpointRounding.ToZero);
                     
                     repo.UpdatePlayerProfile(profile);
                }
            }
            
            if (success) {
                repo.UpdateUser(user);
                
                repo.SaveTransaction(new Transaction {
                    Id = Guid.NewGuid(), 
                    UserId = userId, 
                    Amount = -amount, 
                    Type = TransactionType.Bet, 
                    Description = (user.Role == Role.Admin) ? "Admin Bet" : "Game Bet", 
                    Timestamp = DateTime.UtcNow, 
                    ResultingBalance = user.Balance
                });

                repo.SaveChanges();
                transaction.Commit();
                
                // CACHE INVALIDATION after commit
                SafeFireAndForget(_cache.RemoveAsync($"user:profile:{userId}"), "Cache invalidation");
                SafeFireAndForget(_realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance), "Balance notification");
            } else {
                transaction.Rollback();
            }

            return success;
        }
        catch {
            transaction.Rollback();
            throw;
        }
    }

    public async Task ProcessWinAsync(Guid userId, decimal amount, IGameRepository repo) {
        // SECURITY: Validate decimal input
        if (!ValidationHelper.IsValidDecimal(amount) || amount <= 0) return;
        amount = ValidationHelper.ValidateAndClamp(amount, GameConstants.MIN_BET, GameConstants.MAX_WIN);

        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));
        using var transaction = repo.BeginTransaction();

        try {
            var user = repo.GetUser(userId);
            var profile = repo.GetPlayerProfile(userId);
            
            if (user == null) {
                transaction.Rollback();
                return;
            }

            if (profile != null && user.WageringRequirement == 0) {
                // SECURITY: Round to prevent rounding error exploits
                profile.ShadowBalance -= Math.Round(amount, 2, MidpointRounding.ToZero);
                
                // SECURITY: Cap cashback level at maximum
                int effectiveLevel = Math.Min(profile.CashbackLevel, GameConstants.MAX_CASHBACK_LEVEL);
                decimal rate = GameConstants.BASE_CASHBACK_RATE + (effectiveLevel * GameConstants.CASHBACK_PER_LEVEL);
                decimal cashbackAdjustment = Math.Round(amount * rate, 2, MidpointRounding.ToZero);
                profile.PendingCashback = Math.Max(0, profile.PendingCashback - cashbackAdjustment);

                repo.UpdatePlayerProfile(profile);
            }

            if (user.BonusBalance > 0 && user.WageringProgress < user.WageringRequirement) {
                user.BonusBalance += amount;
            } else {
                user.Balance += amount;
            }

            repo.UpdateUser(user);
            
            repo.SaveTransaction(new Transaction {
                Id = Guid.NewGuid(), UserId = userId, Amount = amount, Type = TransactionType.Win, 
                Description = "Game Win", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
            });

            repo.SaveChanges();
            transaction.Commit();
            
            // CACHE INVALIDATION after commit
            SafeFireAndForget(_cache.RemoveAsync($"user:profile:{userId}"), "Cache invalidation");
            SafeFireAndForget(_realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance), "Balance notification");
        }
        catch {
            transaction.Rollback();
            throw;
        }
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
        using var transaction = repo.BeginTransaction();

        try {
            var user = repo.GetUser(userId);
            if (user == null) {
                transaction.Rollback();
                return;
            }
            
            user.BonusBalance += amount;
            user.WageringRequirement += wageringRequirement;
            user.BonusLastUpdated = DateTime.UtcNow;
            repo.UpdateUser(user);

            repo.SaveTransaction(new Transaction {
                Id = Guid.NewGuid(), UserId = userId, Amount = amount, Type = TransactionType.Bonus, 
                Description = "Bonus Credited", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
            });

            repo.SaveChanges();
            transaction.Commit();
            
            SafeFireAndForget(_realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance), "Bonus credit notification");
        }
        catch {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> CashoutBonusAsync(Guid userId, IGameRepository repo) {
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));
        using var transaction = repo.BeginTransaction();

        try {
            var user = repo.GetUser(userId);
            if (user == null || user.BonusBalance <= 0) {
                transaction.Rollback();
                return false;
            }
            
            // IMPROVED: Progress-based cashout (was harsh 10% flat rate)
            decimal progressPercent = user.WageringRequirement > 0 
                ? Math.Min(user.WageringProgress / user.WageringRequirement, 1.0m)
                : 1.0m;
            
            // Give back 50% base + 50% * progress (so 50%-100% depending on wagering completion)
            decimal cashoutRate = 0.50m + (0.50m * progressPercent);
            decimal amountToCredit = user.BonusBalance * cashoutRate;
            
            user.Balance += amountToCredit;
            user.BonusBalance = 0;
            user.WageringRequirement = 0;
            user.WageringProgress = 0;
            repo.UpdateUser(user);

            if (amountToCredit > 0) {
                repo.SaveTransaction(new Transaction {
                    Id = Guid.NewGuid(), UserId = userId, Amount = amountToCredit, Type = TransactionType.Bonus, 
                    Description = $"Bonus Cashout ({cashoutRate:P0})", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
                });
            }

            repo.SaveChanges();
            transaction.Commit();
            
            SafeFireAndForget(_realTime.NotifyBalanceUpdate(userId, user.Balance, 0), "Cashout notification");
            return true;
        }
        catch {
            transaction.Rollback();
            throw;
        }
    }

    public decimal GetPendingCashback(Guid userId, IGameRepository repo) {
        var profile = repo.GetPlayerProfile(userId);
        return profile?.PendingCashback ?? 0;
    }

    public async Task<decimal> ClaimCashbackAsync(Guid userId, IGameRepository repo) {
        using var lockHandle = await _lockService.AcquireLockAsync($"wallet_{userId}", TimeSpan.FromSeconds(5));
        using var transaction = repo.BeginTransaction();
        
        try {
            var profile = repo.GetPlayerProfile(userId);
            var user = repo.GetUser(userId);
            
            if (profile == null || user == null || profile.PendingCashback < 0.01m) {
                transaction.Rollback();
                return 0;
            }

            // SECURITY: Round to 2 decimal places to prevent micro-transaction exploits
            decimal amount = Math.Round(profile.PendingCashback, 2, MidpointRounding.ToZero);
            profile.PendingCashback = 0;
            user.Balance += amount;
            
            repo.UpdatePlayerProfile(profile);
            repo.UpdateUser(user);

            repo.SaveTransaction(new Transaction {
                Id = Guid.NewGuid(), UserId = userId, Amount = amount, Type = TransactionType.Bonus, 
                Description = "Cashback Claim", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
            });

            repo.SaveChanges();
            transaction.Commit();
            
            SafeFireAndForget(_realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance), "Cashback claim notification");
            return amount;
        }
        catch {
            transaction.Rollback();
            throw;
        }
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