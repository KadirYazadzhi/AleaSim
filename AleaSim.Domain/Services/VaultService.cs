using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class VaultService : IVaultService {
    private readonly IRealTimeService _realTime;
    private readonly object _lock = new();

    public VaultService(IRealTimeService realTime) {
        _realTime = realTime;
    }

    public bool ProcessBet(Guid userId, decimal amount, IGameRepository repo) {
        var user = repo.GetUser(userId);
        if (user == null) return false;

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

        if (success) {
            repo.UpdateUser(user);
            _ = _realTime.NotifyBalanceUpdate(userId, user.Balance + user.BonusBalance);
        }

        return success;
    }

    public void ProcessWin(Guid userId, decimal amount, IGameRepository repo) {
        var user = repo.GetUser(userId);
        if (user == null) return;

        if (user.BonusBalance > 0 && user.WageringProgress < user.WageringRequirement) {
            user.BonusBalance += amount;
        } else {
            user.Balance += amount;
        }

        repo.UpdateUser(user);
        _ = _realTime.NotifyBalanceUpdate(userId, user.Balance + user.BonusBalance);
    }

    public bool CanAffordWin(Guid userId, Guid gameId, decimal winAmount, IGameRepository repo) {
        lock (_lock) {
            var game = repo.GetGame(gameId);
            if (game == null) return false;
            return game.PoolBalance >= winAmount;
        }
    }

    public void CreditBonus(Guid userId, decimal amount, decimal wageringRequirement, IGameRepository repo) {
        var user = repo.GetUser(userId);
        if (user == null) return;

        user.BonusBalance += amount;
        user.WageringRequirement += wageringRequirement;
        user.BonusLastUpdated = DateTime.UtcNow;
        repo.UpdateUser(user);
        _ = _realTime.NotifyBalanceUpdate(userId, user.Balance + user.BonusBalance);
    }

    public bool CashoutBonus(Guid userId, IGameRepository repo) {
        var user = repo.GetUser(userId);
        if (user == null || user.BonusBalance <= 0) return false;

        decimal amountToCredit = (user.BonusBalance >= 100) ? user.BonusBalance * 0.10m : 0;

        user.Balance += amountToCredit;
        user.BonusBalance = 0;
        user.WageringRequirement = 0;
        user.WageringProgress = 0;

        repo.UpdateUser(user);
        _ = _realTime.NotifyBalanceUpdate(userId, user.Balance);
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