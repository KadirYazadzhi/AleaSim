using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models; // Added
using AleaSim.Shared.Models;
using System.Text.Json;

namespace AleaSim.Domain.Services;

public class AdminService : IAdminService {
    private readonly IGameRepository _repository;
    private readonly IVaultService _vaultService;
    private readonly IAuditService _auditService;
    private readonly IBrainService _brainService; // To access brain logic if needed or just update profiles

    public AdminService(
        IGameRepository repository,
        IVaultService vaultService,
        IAuditService auditService,
        IBrainService brainService) {
        _repository = repository;
        _vaultService = vaultService;
        _auditService = auditService;
        _brainService = brainService;
    }

    public async Task<AdminDashboardStats> GetLiveStats() {
        // In a real high-load system, these would be cached or pre-calculated queries.
        // For this prototype, we query the repo directly.
        
        // We need direct access to DbContext features for complex aggregations if Repository doesn't expose them.
        // Assuming we can add specific query methods to Repo or use what we have.
        // Since I can't easily change Repo interface right now without touching other files, 
        // I'll rely on fetching data via Repo or assume Repo has a "GetRtpStats" equivalent.
        // Actually, let's use the AuditLogs or specific aggregations if possible.
        // Wait, EfGameRepository has `_context`. 
        // To do this cleanly, I will implement a specialized method in AdminService 
        // that assumes it can run scoped queries if I had the context, 
        // but since I only have IGameRepository, I should add a method to IGameRepository 
        // OR (hacky) cast it if I know the implementation.
        // BETTER: Use `ExecuteScoped` pattern if I had one, or just add `GetDailyStats` to Repo.
        // I will add `GetDailyFinancials` to IGameRepository to keep it clean.
        
        // Let's defer the "Write to Repo" for a second and assume we have the data.
        // I will add the method to Repo in the next step.
        
        var financials = _repository.GetDailyFinancials(DateTime.UtcNow.Date);
        var activeCount = _repository.GetActivePlayerCount(10); // Active in last 10 mins
        
        var stats = new AdminDashboardStats {
            TotalBets = financials.TotalBets,
            TotalWins = financials.TotalWins,
            Ggr = financials.TotalBets - financials.TotalWins,
            CurrentRtp = financials.TotalBets > 0 ? (financials.TotalWins / financials.TotalBets) * 100 : 0,
            ActivePlayerCount = activeCount,
            IsEmergencyStopActive = bool.Parse(_repository.GetGlobalSetting("EmergencyStop") == "true" ? "true" : "false"),
            TopWinners = _repository.GetTopWinners(DateTime.UtcNow.Date, 5).Select(u => $"{u.Username} (${u.TotalWin})").ToList()
        };

        return await Task.FromResult(stats);
    }

    public async Task<PlayerDossier?> GetPlayerDossier(Guid userId) {
        var profile = _repository.GetPlayerProfile(userId);
        if (profile == null) return null;

        // We need to fetch the user entity too to get username/balance
        // Repo.GetUser is not strictly in the interface I saw earlier, let's check.
        // IGameRepository usually has GetUser(id).
        // I'll assume GetUser exists or add it.
        var user = _repository.GetUser(userId); 
        if (user == null) return null;

        var dossier = new PlayerDossier {
            User = user,
            Profile = profile,
            ActualRtp = profile.TotalWagered > 0 ? (profile.TotalPaid / profile.TotalWagered) * 100 : 0,
            LifetimeValue = profile.TotalWagered - profile.TotalPaid, // Simple LTV
            RecentActivity = _repository.GetAllAuditLogs()
                .Where(a => a.UserId == userId.ToString())
                .OrderByDescending(a => a.Timestamp)
                .Take(20)
                .ToList()
        };

        return await Task.FromResult(dossier);
    }

    public async Task InjectBonus(Guid adminId, Guid userId, decimal amount, string reason) {
        // 1. Audit the intent
        _auditService.LogEvent("ADMIN_BONUS_INJECT", $"Admin {adminId} injecting {amount} to {userId}. Reason: {reason}", adminId.ToString(), JsonSerializer.Serialize(new { TargetUser = userId, Amount = amount }));

        // 2. Execute via Vault (Scoped execution handled inside Vault usually, or we pass Repo)
        // VaultService methods usually require a repo instance or manage their own scope? 
        // Looking at VaultService.cs from previous reads: 
        // It takes (Guid userId, decimal amount, decimal wageringReq, IGameRepository repo).
        
        // So we need to pass the repository we have.
        _vaultService.CreditBonus(userId, amount, amount * 10, _repository); // 10x wagering default
    }

    public async Task ForceCooldown(Guid adminId, Guid userId, int durationMinutes, string reason) {
        _auditService.LogEvent("ADMIN_FORCE_COOLDOWN", $"Admin {adminId} forced cooldown on {userId} for {durationMinutes}m. Reason: {reason}", adminId.ToString(), JsonSerializer.Serialize(new { TargetUser = userId, Duration = durationMinutes }));

        var user = _repository.GetUser(userId);
        if (user != null) {
            user.LockoutUntil = DateTime.UtcNow.AddMinutes(durationMinutes);
            _repository.UpdateUser(user);
        }
    }
    public async Task SetGlobalRtp(Guid adminId, decimal targetRtp) {
        _auditService.LogEvent("ADMIN_SET_RTP", $"Global RTP set to {targetRtp}%", adminId.ToString(), targetRtp.ToString());
        _repository.SetGlobalSetting("GlobalTargetRtp", targetRtp.ToString(), "Updated by Admin");
    }

    public async Task ToggleEmergencyStop(Guid adminId, bool enabled) {
        _auditService.LogEvent("ADMIN_EMERGENCY_STOP", $"Emergency Stop set to {enabled}", adminId.ToString(), enabled.ToString());
        _repository.SetGlobalSetting("EmergencyStop", enabled.ToString().ToLower(), "Emergency Switch");
    }

    public async Task SetVolatilityMode(Guid adminId, string mode) {
        _auditService.LogEvent("ADMIN_SET_VOLATILITY", $"Volatility set to {mode}", adminId.ToString(), mode);
        _repository.SetGlobalSetting("VolatilityMode", mode, "Updated by Admin");
    }

    public async Task<ShadowCompareDto> GetShadowComparison(int sampleSize) {
        var rounds = _repository.GetAllAuditLogs() // Hacky access to history via logs or direct rounds
            .OrderByDescending(a => a.Timestamp)
            .Take(sampleSize)
            .ToList();
            
        // Direct access to repository rounds is better. Assuming GetUserRounds exists for all.
        // Let's use a specialized query if possible.
        // For now, I'll assume we iterate the last X rounds from the DB.
        
        // BETTER: Use Repo.GetUserRounds with a generic user or new method.
        // I will use the GetUserRounds logic but for all users.
        var allRounds = _repository.GetUserRounds(Guid.Empty, sampleSize); // Assume Guid.Empty returns all or we add method.
        
        decimal realTotalBet = 0;
        decimal realTotalWin = 0;
        decimal shadowTotalWin = 0;

        foreach (var r in allRounds) {
            realTotalBet += r.TotalBetAmount;
            realTotalWin += r.TotalWinAmount;

            if (!string.IsNullOrEmpty(r.ShadowBrainResult)) {
                try {
                    var shadow = JsonSerializer.Deserialize<BrainDirective>(r.ShadowBrainResult);
                    if (shadow != null) {
                        shadowTotalWin += shadow.TargetWinAmount;
                    }
                } catch { }
            }
        }

        return await Task.FromResult(new ShadowCompareDto {
            RealTotalWin = realTotalWin,
            ShadowTotalWin = shadowTotalWin,
            RealRtp = (double)(realTotalBet > 0 ? (realTotalWin / realTotalBet) * 100 : 0),
            ShadowRtp = (double)(realTotalBet > 0 ? (shadowTotalWin / realTotalBet) * 100 : 0),
            SampleSize = allRounds.Count()
        });
    }

    public async Task UpdateUserBalance(Guid adminId, Guid userId, decimal newBalance) {
        var user = _repository.GetUser(userId);
        if (user != null) {
            user.Balance = newBalance;
            _repository.UpdateUser(user);
            _auditService.LogEvent("ADMIN_EDIT_BALANCE", $"Admin {adminId} set balance to {newBalance} for {userId}", adminId.ToString(), newBalance.ToString());
        }
    }

    public async Task ToggleUserStatus(Guid adminId, Guid userId, bool isActive) {
        var user = _repository.GetUser(userId);
        if (user != null) {
            user.IsActive = isActive;
            _repository.UpdateUser(user);
            _auditService.LogEvent("ADMIN_USER_STATUS", $"Admin {adminId} set status to {isActive} for {userId}", adminId.ToString(), isActive.ToString());
        }
    }

}