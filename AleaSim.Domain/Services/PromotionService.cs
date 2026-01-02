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
        
        // Update User Activity Timestamp
        var user = repo.GetUser(userId);
        if (user != null) {
            user.LastBetTimestamp = DateTime.UtcNow;
            repo.UpdateUser(user);
        }

        // --- TOURNAMENT TRACKING ---
        // Strictly on the 30th
        if (DateTime.UtcNow.Day == 30) {
            var entry = repo.GetOrCreateTournamentEntry(userId, DateTime.UtcNow);
            entry.TotalWagered += betAmount;
            entry.RoundCount++;
            repo.UpdateTournamentEntry(entry);
        }
    }
    
    // Helper to record Win (needs to be called from Engine too)
    // Actually, ProcessBetActivity is called at BET time. We don't know the WIN yet.
    // We need a new method: ProcessWinActivity
    
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
        
        // Active if bet within last 3 minutes
        return user.LastBetTimestamp > DateTime.UtcNow.AddMinutes(-3);
    }

    public async Task ExecuteRaffleDraw(decimal prizeAmount, string raffleType, IGameRepository repo) {
        // 1. Get Eligible Candidates (Active + Have Tickets)
        var activeProfiles = repo.GetActiveProfiles(TimeSpan.FromMinutes(3));
        
        // Pick a winner using Weighted Random (Tickets)
        var winnerId = PickWinner(activeProfiles);
        
        if (winnerId != Guid.Empty) {
            // Credit Bonus (Locked)
            _vaultService.CreditBonus(winnerId, prizeAmount, prizeAmount, repo); // 1x Wagering
            
            // Notify User
            await _realTimeService.NotifyGameUpdate(winnerId, new {
                Type = "RaffleWin",
                Amount = prizeAmount,
                Message = $"You won the {raffleType} Raffle!"
            });
        }
    }

    private Guid PickWinner(IEnumerable<PlayerProfile> profiles) {
        var profileList = profiles.ToList();
        if (!profileList.Any()) return Guid.Empty;

        // Calculate Tickets: 1 Ticket per 50 units wagered
        // Use a Dictionary or Tuple list to store Ticket thresholds
        // Or simpler: Cumulative Probability
        
        decimal totalTickets = profileList.Sum(p => Math.Floor(p.MonthlyWagered / 50m));
        
        if (totalTickets <= 0) {
            // No one has tickets? Fallback to random uniform pick among actives
            return profileList[new Random().Next(profileList.Count)].UserId;
        }

        // Random point between 0 and TotalTickets
        // Random.NextDouble() returns 0..1
        decimal winningTicket = (decimal)new Random().NextDouble() * totalTickets;
        
        decimal currentTicketCount = 0;
        foreach (var p in profileList) {
            decimal tickets = Math.Floor(p.MonthlyWagered / 50m);
            if (tickets <= 0) continue;

            currentTicketCount += tickets;
            
            // If the counter crosses the winning ticket number, this user wins
            if (currentTicketCount >= winningTicket) {
                return p.UserId;
            }
        }

        // Fallback (should theoretically not reach here due to math)
        return profileList.Last().UserId;
    }
}
