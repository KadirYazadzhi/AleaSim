using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Domain.Services;

public class PromotionService : IPromotionService {
    private readonly IRealTimeService _realTimeService;
    private readonly IVaultService _vaultService;

    public PromotionService(IRealTimeService realTimeService, IVaultService vaultService) {
        _realTimeService = realTimeService;
        _vaultService = vaultService;
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

        if (DateTime.UtcNow.Day == 30) {
            var entry = repo.GetOrCreateTournamentEntry(userId, DateTime.UtcNow);
            entry.TotalWagered += betAmount;
            entry.RoundCount++;
            repo.UpdateTournamentEntry(entry);
        }
    }
    
    public void ProcessWinActivity(Guid userId, decimal winAmount, IGameRepository repo) {
        if (DateTime.UtcNow.Day == 30) {
            var entry = repo.GetOrCreateTournamentEntry(userId, DateTime.UtcNow);
            entry.TotalPayout += winAmount;
            repo.UpdateTournamentEntry(entry);
        }
    }

    public bool IsUserActive(Guid userId, IGameRepository repo) {
        var user = repo.GetUser(userId);
        if (user == null || user.LastBetTimestamp == null) return false;
        return user.LastBetTimestamp > DateTime.UtcNow.AddMinutes(-3);
    }

    public async Task ExecuteRaffleDraw(decimal prizeAmount, string raffleType, IGameRepository repo) {
        var activeProfiles = repo.GetActiveProfiles(TimeSpan.FromMinutes(3));
        var winnerId = PickWinner(activeProfiles);
        
        if (winnerId != Guid.Empty) {
            _vaultService.CreditBonus(winnerId, prizeAmount, prizeAmount, repo);
            await _realTimeService.NotifyGameUpdate(winnerId, new {
                Type = "RaffleWin",
                Amount = prizeAmount,
                Message = $"You won the {raffleType} Raffle!"
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
        var rnd = new Random();
        int roll = rnd.Next(1, 101);
        
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

    private Guid PickWinner(IEnumerable<PlayerProfile> profiles) {
        var profileList = profiles.ToList();
        if (!profileList.Any()) return Guid.Empty;

        decimal totalTickets = profileList.Sum(p => Math.Floor(p.MonthlyWagered / 50m));
        if (totalTickets <= 0) return profileList[new Random().Next(profileList.Count)].UserId;

        decimal winningTicket = (decimal)new Random().NextDouble() * totalTickets;
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