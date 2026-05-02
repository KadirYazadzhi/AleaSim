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
    private readonly IBackgroundTaskQueue _taskQueue;

    public VaultService(IRealTimeService realTime, ILockService lockService, IRedisCacheService cache, ILogger<VaultService> logger, IBackgroundTaskQueue taskQueue) {
        _realTime = realTime;
        _lockService = lockService;
        _cache = cache;
        _logger = logger;
        _taskQueue = taskQueue;
    }
    
    // IMPROVEMENT: Proper background task queue pattern (Issue 2)
    private async Task EnqueueTask(Task task, string operationName) {
        await _taskQueue.QueueBackgroundWorkItemAsync(async (ct) => {
            try {
                await task;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Background task failed: {Operation}", operationName);
            }
        });
    }

    public async Task<bool> ProcessBetAsync(Guid userId, decimal amount, IGameRepository repo, Guid? referenceId = null) {
        _logger.LogDebug($"ProcessBetAsync START: userId={userId}, amount={amount}");
        
        // SECURITY: Validate decimal input (reject special values, not range)
        if (!ValidationHelper.IsValidDecimal(amount) || amount <= 0) {
            _logger.LogWarning($"Invalid bet amount: {amount}");
            return false; // Reject gracefully
        }
        
        // SECURITY: Hard cap at absolute maximum (prevent overflow attacks)
        if (amount > GameConstants.MAX_BET) {
            _logger.LogWarning($"Bet amount {amount} exceeds maximum {GameConstants.MAX_BET}");
            return false; // Reject bets over 1 million
        }

        // 0. IDEMPOTENCY CHECK (Issue 31)
        if (referenceId.HasValue) {
            var existing = repo.GetTransaction(referenceId.Value);
            if (existing != null) {
                _logger.LogWarning($"Duplicate ProcessBet request for userId {userId}, referenceId {referenceId}");
                return true; // Already processed, treat as success
            }
        }
        
        // NOTE: Lock is now managed by the caller (BaseGameEngine) to cover the entire transaction
        var user = repo.GetUser(userId);
        var profile = repo.GetPlayerProfile(userId);
        if (user == null) {
            return false;
        }

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

            if (user.BonusBalance > 0) {
                if (user.BonusBalance >= amount) {
                    user.BonusBalance = (user.BonusBalance - amount).RoundForStorage();
                    if (user.WageringRequirement > 0) {
                        user.WageringProgress = (user.WageringProgress + amount).RoundForStorage();
                        CheckWageringCompletion(user);
                    }
                    success = true;
                }
                else {
                    decimal remainder = (amount - user.BonusBalance).RoundForStorage();
                    decimal bonusPart = user.BonusBalance;
                    user.BonusBalance = 0;
                    if (user.WageringRequirement > 0) {
                            user.WageringProgress = (user.WageringProgress + bonusPart).RoundForStorage();
                            CheckWageringCompletion(user);
                    }
                    if (user.Balance >= remainder) {
                        user.Balance = (user.Balance - remainder).RoundForStorage();
                        success = true;
                    }
                }
            }
            else if (user.Balance >= amount) {
                user.Balance = (user.Balance - amount).RoundForStorage();
                success = true;
            }

            if (success && profile != null) {
                if (user.WageringRequirement == 0) {
                     // Apply centralized rounding policy for consistency
                     profile.ShadowBalance = (profile.ShadowBalance + (amount * GameConstants.SHADOW_BALANCE_RATE)).RoundForStorage();
                     
                     // SECURITY: Cap cashback level at maximum
                     int effectiveLevel = Math.Min(profile.CashbackLevel, GameConstants.MAX_CASHBACK_LEVEL);
                     decimal rate = GameConstants.BASE_CASHBACK_RATE + (effectiveLevel * GameConstants.CASHBACK_PER_LEVEL);
                     profile.PendingCashback = (profile.PendingCashback + amount.MultiplyByPercent(rate * 100m)).RoundForStorage();
                     
                     repo.UpdatePlayerProfile(profile);
                }
            }
            
            if (success) {
                repo.UpdateUser(user);
                
                repo.SaveTransaction(new Transaction {
                    Id = referenceId ?? Guid.NewGuid(), 
                    UserId = userId, 
                    Amount = -amount, 
                    Type = TransactionType.Bet, 
                    Description = "Game Bet", 
                    Timestamp = DateTime.UtcNow, 
                    ResultingBalance = user.Balance
                });

                // NOTE: SaveChanges() and Commit() will be called by BaseGameEngine
                // We just prepare the entities for commit
                
                // CACHE INVALIDATION (fire and forget - no transaction dependency)
                // FIXED (Issue 26): Use background queue for both to avoid awaiting inside the transaction/lock
                await EnqueueTask(_cache.RemoveAsync($"user:profile:{userId}"), "Cache invalidation");
                await EnqueueTask(_realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance), "Balance update notification");
            }

            return success;
    }

    public async Task ProcessWinAsync(Guid userId, decimal amount, IGameRepository repo, Guid? referenceId = null) {
        // SECURITY: Validate decimal input (reject special values)
        if (!ValidationHelper.IsValidDecimal(amount) || amount <= 0) return;
        
        // SECURITY: Hard cap at absolute maximum win
        if (amount > GameConstants.MAX_WIN) {
            _logger.LogWarning($"Win amount {amount} exceeds maximum {GameConstants.MAX_WIN}, capping");
            amount = GameConstants.MAX_WIN;
        }

        // NOTE: Lock is managed by caller (BaseGameEngine)
        if (referenceId.HasValue) {
            var existing = repo.GetTransaction(referenceId.Value);
            if (existing != null) {
                _logger.LogWarning($"Duplicate ProcessWin request for userId {userId}, referenceId {referenceId}");
                return; // Already processed
            }
        }

        var user = repo.GetUser(userId);
        var profile = repo.GetPlayerProfile(userId);
        
        if (user == null) {
            return;
        }

        if (profile != null && user.WageringRequirement == 0) {
            // Apply centralized rounding policy
            profile.ShadowBalance = (profile.ShadowBalance - amount).RoundForStorage();
            
            // SECURITY: Cap cashback level at maximum
            int effectiveLevel = Math.Min(profile.CashbackLevel, GameConstants.MAX_CASHBACK_LEVEL);
            decimal rate = GameConstants.BASE_CASHBACK_RATE + (effectiveLevel * GameConstants.CASHBACK_PER_LEVEL);
            decimal cashbackAdjustment = amount.MultiplyByPercent(rate * 100m);
            profile.PendingCashback = Math.Max(0, (profile.PendingCashback - cashbackAdjustment).RoundForStorage());

            repo.UpdatePlayerProfile(profile);
        }

        if (user.BonusBalance > 0 && user.WageringProgress < user.WageringRequirement) {
            user.BonusBalance = (user.BonusBalance + amount).RoundForStorage();
        } else {
            user.Balance = (user.Balance + amount).RoundForStorage();
        }

        repo.UpdateUser(user);
        
        repo.SaveTransaction(new Transaction {
            Id = referenceId ?? Guid.NewGuid(), // Use referenceId as Transaction ID for strict DB idempotency
            UserId = userId, Amount = amount, Type = TransactionType.Win, 
            Description = "Game Win", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
        });

        // NOTE: SaveChanges() and Commit() will be called by caller
        
        // CACHE INVALIDATION (fire and forget)
        await EnqueueTask(_cache.RemoveAsync($"user:profile:{userId}"), "Cache invalidation");
        await EnqueueTask(_realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance), "Balance update notification");
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

        public async Task CreditBonusAsync(Guid userId, decimal amount, decimal wageringRequirement, IGameRepository repo) {        if (amount <= 0) return;
        
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
            
            await EnqueueTask(_realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance), "Bonus credit notification");
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
            
            await EnqueueTask(_realTime.NotifyBalanceUpdate(userId, user.Balance, 0), "Cashout notification");
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

            // SECURITY: Round to 2 decimal places using AwayFromZero to be fair to the player (Issue 37)
            decimal amount = Math.Round(profile.PendingCashback, 2, MidpointRounding.AwayFromZero);
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
            
            await _realTime.NotifyBalanceUpdate(userId, user.Balance, user.BonusBalance).ConfigureAwait(false);
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