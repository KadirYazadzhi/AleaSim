using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Domain.Services;

public class PromotionService : IPromotionService {
    private readonly IRealTimeService _realTimeService;
    private readonly IVaultService _vaultService;
    private readonly IRngService _rngService;

    public PromotionService(IRealTimeService realTimeService, IVaultService vaultService, IRngService rngService) {
        _realTimeService = realTimeService;
        _vaultService = vaultService;
        _rngService = rngService;
    }

    public void ProcessBetActivity(Guid userId, decimal betAmount, IGameRepository repo) {
        var profile = repo.GetPlayerProfile(userId);
        if (profile != null) {
            profile.WeeklyWagered += betAmount;
            profile.MonthlyWagered += betAmount;
            repo.UpdatePlayerProfile(profile);
        }
        
        var user = repo.GetUser(userId);
        if (user != null) {
            user.LastBetTimestamp = DateTime.UtcNow;
            repo.UpdateUser(user);
        }

        // Always update tournament stats for today
        var entry = repo.GetOrCreateTournamentEntry(userId, DateTime.UtcNow);
        entry.TotalWagered += betAmount;
        entry.RoundCount++;
        repo.UpdateTournamentEntry(entry);
    }
    
    public void ProcessWinActivity(Guid userId, decimal winAmount, IGameRepository repo) {
        // Always update tournament stats
        var entry = repo.GetOrCreateTournamentEntry(userId, DateTime.UtcNow);
        entry.TotalPayout += winAmount;
        repo.UpdateTournamentEntry(entry);
    }

    public bool IsUserActive(Guid userId, IGameRepository repo) {
        var user = repo.GetUser(userId);
        if (user == null || user.LastBetTimestamp == null) return false;
        return user.LastBetTimestamp > DateTime.UtcNow.AddMinutes(-3);
    }

    public async Task ExecuteRaffleDraw(decimal prizeAmount, string raffleType, IGameRepository repo) {
        // Get all active profiles from the last 10 minutes to have a candidate pool
        var activeProfiles = repo.GetActiveProfiles(TimeSpan.FromMinutes(10)).ToList();
        
        Guid winnerId = Guid.Empty;
        int attempts = 0;

        // Re-roll Logic: Try up to 10 times to find a strictly active/online user
        while (attempts < 10) {
            var candidateId = PickWinner(activeProfiles);
            if (candidateId == Guid.Empty) break;

            // Strict check: Must have bet in last 3 minutes
            if (IsUserActive(candidateId, repo)) {
                winnerId = candidateId;
                break;
            }
            
            // Remove candidate from pool to avoid re-picking same inactive user
            activeProfiles.RemoveAll(p => p.UserId == candidateId);
            attempts++;
        }
        
        if (winnerId != Guid.Empty) {
            _vaultService.CreditBonus(winnerId, prizeAmount, prizeAmount, repo);
            await _realTimeService.NotifyGameUpdate(winnerId, new {
                Type = "RaffleWin",
                Amount = prizeAmount,
                Message = $"🎉 You won the {raffleType} Raffle!"
            });
        }
    }

    public async Task<object> SpinBonusWheel(Guid userId, IGameRepository repo) {
        var user = repo.GetUser(userId);
        if (user == null) throw new Exception("User not found");

        if (user.LastDailySpin.HasValue && user.LastDailySpin.Value.Date == DateTime.UtcNow.Date) {
            throw new Exception("You already used your daily spin!");
        }

        user.LastDailySpin = DateTime.UtcNow;
        int roll = _rngService.GetNextInt((int)DateTime.UtcNow.Ticks, 100, 1, 101);
        
        string type; decimal value; string message;

        if (roll <= 10) { 
            type = "BonusCash"; value = 50m;
            _vaultService.CreditBonus(userId, value, value * 5, repo);
            message = "$50.00 Bonus Credited!";
        }
        else if (roll <= 40) { 
            type = "XP"; value = 500m;
            message = "500 XP Gained!";
        }
        else { 
            type = "BonusCash"; value = 5m;
            _vaultService.CreditBonus(userId, value, value, repo);
            message = "$5.00 Bonus Credited!";
        }

        repo.UpdateUser(user);
        return new { Type = type, Value = value, Message = message };
    }

    public async Task<object> ClaimDailyStreakReward(Guid userId, IGameRepository repo) {
        var user = repo.GetUser(userId);
        if (user == null) throw new Exception("User not found");

        var now = DateTime.UtcNow.Date;
        if (user.LastStreakClaim.HasValue && user.LastStreakClaim.Value.Date == now) {
            throw new Exception("Already claimed today's reward!");
        }

        // Calculate Streak
        if (user.LastStreakClaim.HasValue && user.LastStreakClaim.Value.Date == now.AddDays(-1)) {
            user.CurrentStreak++;
        } else {
            user.CurrentStreak = 1;
        }

        user.LastStreakClaim = DateTime.UtcNow;
        
        // Reward: $1 * Streak (capped at $50)
        decimal reward = Math.Min(user.CurrentStreak * 1.0m, 50m);
        
        _vaultService.CreditBonus(userId, reward, reward, repo); // 1x Wagering
        repo.UpdateUser(user);

        return await Task.FromResult(new { 
            Streak = user.CurrentStreak, 
            Amount = reward, 
            Message = $"Day {user.CurrentStreak} Streak! ${reward} Bonus added." 
        });
    }

    private Guid PickWinner(IEnumerable<PlayerProfile> profiles) {
        var profileList = profiles.ToList();
        if (!profileList.Any()) return Guid.Empty;

        decimal totalTickets = profileList.Sum(p => Math.Floor(p.MonthlyWagered / 50m));
        if (totalTickets <= 0) return profileList[_rngService.GetNextInt((int)DateTime.UtcNow.Ticks, 0, 0, profileList.Count)].UserId;

        decimal winningTicket = (decimal)_rngService.GetNextDouble((int)DateTime.UtcNow.Ticks, 1) * totalTickets;
        decimal currentTicketCount = 0;
        foreach (var p in profileList) {
            decimal tickets = Math.Floor(p.MonthlyWagered / 50m);
            if (tickets <= 0) continue;
            currentTicketCount += tickets;
            if (currentTicketCount >= winningTicket) return p.UserId;
        }
        return profileList.Last().UserId;
    }
}