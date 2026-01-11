using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;

namespace AleaSim.Domain.Services;

public class VaultService : IVaultService {
    private readonly IRealTimeService _realTime;
    private readonly object _lock = new();

    public VaultService(IRealTimeService realTime) {
        _realTime = realTime;
    }

    public bool ProcessBet(Guid userId, decimal amount, IGameRepository repo) {
        if (amount < 0) return false;
        lock (_lock) {
            using var tx = repo.BeginTransaction();
            try {
                var user = repo.GetUser(userId);
                var profile = repo.GetPlayerProfile(userId);
                if (user == null) return false;

                if (user.Role == Role.Admin) {
                     _ = _realTime.NotifyBalanceUpdate(userId, user.Balance + user.BonusBalance);
                    return true;
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

                if (success) {
                    if (profile != null) {
                        profile.ShadowBalance += amount * 0.95m;
                        repo.UpdatePlayerProfile(profile);
                    }
                    repo.UpdateUser(user);
                    
                    // Financial Log
                    repo.SaveTransaction(new Transaction {
                        Id = Guid.NewGuid(), UserId = userId, Amount = -amount, Type = TransactionType.Bet, 
                        Description = "Game Bet", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
                    });

                    tx.Commit();
                    _ = _realTime.NotifyBalanceUpdate(userId, user.Balance + user.BonusBalance);
                }
                return success;
            } catch {
                tx.Rollback();
                return false;
            }
        }
    }

    public void ProcessWin(Guid userId, decimal amount, IGameRepository repo) {
        if (amount <= 0) return;
        lock (_lock) {
            using var tx = repo.BeginTransaction();
            try {
                var user = repo.GetUser(userId);
                var profile = repo.GetPlayerProfile(userId);
                if (user == null) return;

                if (profile != null) {
                    profile.ShadowBalance -= amount;
                    repo.UpdatePlayerProfile(profile);
                }

                if (user.BonusBalance > 0 && user.WageringProgress < user.WageringRequirement) {
                    user.BonusBalance += amount;
                } else {
                    user.Balance += amount;
                }

                repo.UpdateUser(user);

                // Financial Log
                repo.SaveTransaction(new Transaction {
                    Id = Guid.NewGuid(), UserId = userId, Amount = amount, Type = TransactionType.Win, 
                    Description = "Game Win", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
                });

                tx.Commit();
                _ = _realTime.NotifyBalanceUpdate(userId, user.Balance + user.BonusBalance);
            } catch {
                tx.Rollback();
                throw; // Re-throw to inform caller
            }
        }
    }

    public bool CanAffordWin(Guid userId, Guid gameId, decimal winAmount, IGameRepository repo, bool strictShadowCheck = true) {
        lock (_lock) {
            var game = repo.GetGame(gameId);
            var profile = repo.GetPlayerProfile(userId);
            if (game == null) return false;
            bool casinoCanAfford = game.PoolBalance >= winAmount;
            bool userHasShadowCredit = profile == null || profile.ShadowBalance >= winAmount;
            if (!strictShadowCheck) return casinoCanAfford;
            return casinoCanAfford && userHasShadowCredit;
        }
    }

    public void CreditBonus(Guid userId, decimal amount, decimal wageringRequirement, IGameRepository repo) {
        if (amount <= 0) return;
        lock (_lock) {
            using var tx = repo.BeginTransaction();
            try {
                var user = repo.GetUser(userId);
                if (user == null) return;
                user.BonusBalance += amount;
                user.WageringRequirement += wageringRequirement;
                user.BonusLastUpdated = DateTime.UtcNow;
                repo.UpdateUser(user);

                // Financial Log
                repo.SaveTransaction(new Transaction {
                    Id = Guid.NewGuid(), UserId = userId, Amount = amount, Type = TransactionType.Bonus, 
                    Description = "Bonus Credited", Timestamp = DateTime.UtcNow, ResultingBalance = user.Balance
                });

                tx.Commit();
                _ = _realTime.NotifyBalanceUpdate(userId, user.Balance + user.BonusBalance);
            } catch {
                tx.Rollback();
            }
        }
    }

    public bool CashoutBonus(Guid userId, IGameRepository repo) {
        lock (_lock) {
            using var tx = repo.BeginTransaction();
            try {
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

                tx.Commit();
                _ = _realTime.NotifyBalanceUpdate(userId, user.Balance);
                return true;
            } catch {
                tx.Rollback();
                return false;
            }
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
