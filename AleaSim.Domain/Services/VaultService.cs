using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;

namespace AleaSim.Domain.Services;

public class VaultService : IVaultService {
    private readonly IRealTimeService _realTime;
    private readonly ILockService _lockService;

    public VaultService(IRealTimeService realTime, ILockService lockService) {
        _realTime = realTime;
        _lockService = lockService;
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
                // Only contribute to Shadow Balance if playing with Real Money
                // If BonusBalance was used (partially or fully), we skip contribution to avoid skewing Real RTP with Bonus Wagering
                bool isRealMoneyBet = user.BonusBalance == 0 || (user.BonusBalance > 0 && user.BonusBalance < amount); // Simplified: If any bonus used, treat as bonus? Or proportional?
                // Strict approach: If ANY bonus money used, don't accure Shadow.
                // However, the original code deduction logic was:
                // if (user.BonusBalance >= amount) -> Pure Bonus
                // else -> Mixed or Pure Real.
                
                // Let's rely on the state BEFORE deduction. We've already deducted.
                // Re-evaluating:
                // If we deducted from BonusBalance, we shouldn't add to ShadowBalance.
                // If we deducted from Balance, we SHOULD add.
                // But `ProcessBetAsync` modifies `user` object in place.
                
                // Better Logic:
                // We know `amount`. We know if we touched `Balance`.
                // The deduction logic above did:
                // if (Bonus >= amount) -> Bonus paid all.
                // else -> Bonus paid some, Balance paid remainder.
                
                // We should only credit Shadow for the portion paid by Real Balance.
                // BUT, `ProcessWinAsync` doesn't know the split.
                // To keep it symmetrical with the proposed `ProcessWinAsync` fix (checking BonusBalance > 0),
                // we will stick to: Only add to Shadow if NO bonus balance existed or was used.
                
                // Actually, simplest and safest given the instructions:
                // "If Bonus money was used ... DO NOT add".
                // We can check if `user.BonusBalance` was involved.
                // Since we already modified `user`, we can't check previous state easily without tracking it.
                // But we can check `transaction` log or just use the logic:
                // If `user.WageringRequirement > 0`, they are in bonus mode.
                
                if (user.WageringRequirement == 0) {
                     profile.ShadowBalance += amount * 0.95m;
                     repo.UpdatePlayerProfile(profile);
                }
            }
            
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