using AleaSim.Domain.Models;

namespace AleaSim.Domain.Interfaces;

public interface IAdminService {
    // Dashboard
    Task<AdminDashboardStats> GetLiveStats();
    
    // Player Inspection & Intervention
    Task<PlayerDossier?> GetPlayerDossier(Guid userId);
    Task InjectBonus(Guid adminId, Guid userId, decimal amount, string reason);
    Task ForceCooldown(Guid adminId, Guid userId, int durationMinutes, string reason);
    
    // System Control
    Task SetGlobalRtp(Guid adminId, decimal targetRtp);
    Task ToggleEmergencyStop(Guid adminId, bool enabled);
    Task SetVolatilityMode(Guid adminId, string mode);
}
