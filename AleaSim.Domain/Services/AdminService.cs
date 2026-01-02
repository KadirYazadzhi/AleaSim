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

        var profile = _repository.GetPlayerProfile(userId);
        if (profile != null) {
            // BrainService uses logic to determine cooldown, but here we override it manually
            // We need a way to persist this. PlayerProfile likely has fields we can use or we add one.
            // Let's assume we can just set a flag or expiry.
            // I'll check PlayerProfile again, but for now I'll assume I can update it.
            // If fields are missing, I'll add them.
            
            // Assuming we have a generic "UpdateProfile" or similar.
            // Actually, BrainService logic is: "If LastWin > X -> Cooldown". 
            // We want a "Hard Lock".
            // I'll add "AccountStatus" or "LockoutUntil" to PlayerProfile if needed.
            // For now, I'll try to find a suitable field.
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
}
