using AleaSim.Domain.Models;
using AleaSim.Shared.Models;

namespace AleaSim.Domain.Interfaces;

public interface IAdminService {
    // Dashboard
    Task<AdminDashboardStats> GetLiveStats();
    Task<AdminDashboardStats> GetStatsForPeriod(string period);
    
    // Player Inspection & Intervention
    Task<PlayerDossierDto?> GetPlayerDossier(Guid userId);
    Task InjectBonus(Guid adminId, Guid userId, decimal amount, string reason);
    Task ForceCooldown(Guid adminId, Guid userId, int durationMinutes, string reason);
    Task KillSession(Guid adminId, Guid userId);
    Task UpdatePlayerNotes(Guid adminId, Guid userId, string notes);
    
    // System Control
    Task SetGlobalRtp(Guid adminId, decimal targetRtp);
    Task ToggleEmergencyStop(Guid adminId, bool enabled);
    Task SetVolatilityMode(Guid adminId, string mode);
    Task<ShadowCompareDto> GetShadowComparison(int sampleSize);
    Task UpdateUserBalance(Guid adminId, Guid userId, decimal newBalance);
    Task ToggleUserStatus(Guid adminId, Guid userId, bool isActive);
    Task ExecuteAction(Guid adminId, string actionType);

}
