using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models; // Added
using AleaSim.Shared.Models;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace AleaSim.Domain.Services;

public class AdminService : IAdminService {
    private readonly IGameRepository _repository;
    private readonly IVaultService _vaultService;
    private readonly IAuditService _auditService;
    private readonly IBrainService _brainService;
    private readonly IRealTimeService _realTime;
    private readonly IMemoryCache _cache;

    public AdminService(
        IGameRepository repository,
        IVaultService vaultService,
        IAuditService auditService,
        IBrainService brainService,
        IRealTimeService realTime,
        IMemoryCache cache) {
        _repository = repository;
        _vaultService = vaultService;
        _auditService = auditService;
        _brainService = brainService;
        _realTime = realTime;
        _cache = cache;
    }

    public Task<AdminDashboardStats> GetLiveStats() {
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

        return Task.FromResult(stats);
    }

    public Task<PlayerDossier?> GetPlayerDossier(Guid userId) {
        var profile = _repository.GetPlayerProfile(userId);
        if (profile == null) return Task.FromResult<PlayerDossier?>(null);

        var user = _repository.GetUser(userId); 
        if (user == null) return Task.FromResult<PlayerDossier?>(null);

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

        return Task.FromResult<PlayerDossier?>(dossier);
    }

    public async Task InjectBonus(Guid adminId, Guid userId, decimal amount, string reason) {
        // 1. Audit the intent
        _auditService.LogEvent("ADMIN_BONUS_INJECT", $"Admin {adminId} injecting {amount} to {userId}. Reason: {reason}", adminId.ToString(), JsonSerializer.Serialize(new { TargetUser = userId, Amount = amount }));

        // 2. Execute via Vault (Scoped execution handled inside Vault usually, or we pass Repo)
        // VaultService methods usually require a repo instance or manage their own scope? 
        // Looking at VaultService.cs from previous reads: 
        // It takes (Guid userId, decimal amount, decimal wageringReq, IGameRepository repo).
        
        // So we need to pass the repository we have.
        await _vaultService.CreditBonusAsync(userId, amount, amount * 10, _repository); // 10x wagering default
    }

    public Task ForceCooldown(Guid adminId, Guid userId, int durationMinutes, string reason) {
        _auditService.LogEvent("ADMIN_FORCE_COOLDOWN", $"Admin {adminId} forced cooldown on {userId} for {durationMinutes}m. Reason: {reason}", adminId.ToString(), JsonSerializer.Serialize(new { TargetUser = userId, Duration = durationMinutes }));

        var user = _repository.GetUser(userId);
        if (user != null) {
            user.LockoutUntil = DateTime.UtcNow.AddMinutes(durationMinutes);
            _repository.UpdateUser(user);
        }
        return Task.CompletedTask;
    }

    public Task SetGlobalRtp(Guid adminId, decimal targetRtp) {
        _auditService.LogEvent("ADMIN_SET_RTP", $"Global RTP set to {targetRtp}%", adminId.ToString(), targetRtp.ToString());
        _repository.SetGlobalSetting("GlobalTargetRtp", targetRtp.ToString(), "Updated by Admin");
        return Task.CompletedTask;
    }

    public Task ToggleEmergencyStop(Guid adminId, bool enabled) {
        _auditService.LogEvent("ADMIN_EMERGENCY_STOP", $"Emergency Stop set to {enabled}", adminId.ToString(), enabled.ToString());
        _repository.SetGlobalSetting("EmergencyStop", enabled.ToString().ToLower(), "Emergency Switch");
        return Task.CompletedTask;
    }

    public Task SetVolatilityMode(Guid adminId, string mode) {
        _auditService.LogEvent("ADMIN_SET_VOLATILITY", $"Volatility set to {mode}", adminId.ToString(), mode);
        _repository.SetGlobalSetting("VolatilityMode", mode, "Updated by Admin");
        return Task.CompletedTask;
    }

    public Task<ShadowCompareDto> GetShadowComparison(int sampleSize) {
        var allRounds = _repository.GetGlobalRecentRounds(sampleSize);
        
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

        return Task.FromResult(new ShadowCompareDto {
            RealTotalWin = realTotalWin,
            ShadowTotalWin = shadowTotalWin,
            RealRtp = (double)(realTotalBet > 0 ? (realTotalWin / realTotalBet) * 100 : 0),
            ShadowRtp = (double)(realTotalBet > 0 ? (shadowTotalWin / realTotalBet) * 100 : 0),
            SampleSize = allRounds.Count()
        });
    }

    public Task UpdateUserBalance(Guid adminId, Guid userId, decimal newBalance) {
        var user = _repository.GetUser(userId);
        if (user != null) {
            user.Balance = newBalance;
            _repository.UpdateUser(user);
            _auditService.LogEvent("ADMIN_EDIT_BALANCE", $"Admin {adminId} set balance to {newBalance} for {userId}", adminId.ToString(), newBalance.ToString());
        }
        return Task.CompletedTask;
    }

    public Task ToggleUserStatus(Guid adminId, Guid userId, bool isActive) {
        var user = _repository.GetUser(userId);
        if (user != null) {
            user.IsActive = isActive;
            _repository.UpdateUser(user);
            _auditService.LogEvent("ADMIN_USER_STATUS", $"Admin {adminId} set status to {isActive} for {userId}", adminId.ToString(), isActive.ToString());
        }
        return Task.CompletedTask;
    }

    public async Task ExecuteAction(Guid adminId, string actionType) {
        _auditService.LogEvent("ADMIN_ACTION", $"Triggered: {actionType}", adminId.ToString(), "{}");
        
        switch(actionType) {
            case "ClearCache":
                // Hacky way to clear cache if we don't have direct access to clear all
                if (_cache is MemoryCache mc) mc.Compact(1.0); 
                break;
            case "GlobalAlert":
                // Assuming RealTimeService has Broadcast
                break;
            case "BackupDb":
                // Mock backup
                await Task.Delay(500);
                break;
            case "BlockIp":
                // Mock blocking
                break;
        }
    }

}