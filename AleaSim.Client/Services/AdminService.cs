using AleaSim.Shared.Models;
using System.Net.Http.Json;

namespace AleaSim.Client.Services;

public interface IAdminService
{
    Task<AdminDashboardStats> GetDashboardStats();
    Task<List<RtpTrendPoint>> GetRtpTrend();
    Task<ShadowCompareDto> GetShadowStats();
    Task<List<ActiveSessionDto>> GetActiveSessions();
    Task UpdateRtp(decimal targetRtp);
    Task ToggleEmergencyStop(bool enabled);
    Task<IntegrityResponse> VerifyIntegrity();
    Task<SimulationReport?> RunSimulation(SimulationRequest request);
    Task UpdateUserBalance(Guid userId, decimal newBalance);
    Task ToggleUserStatus(Guid userId, bool isActive);
    Task UpdateVolatility(string mode);
    Task<List<PlayerSearchResultDto>> SearchPlayers(string query);
    Task TriggerAction(string actionType);
    Task<List<AuditLogDto>> GetAuditLogs();
    Task<List<SupportMessageDto>> GetSupportMessages(int count = 50);
    Task MarkSupportMessageRead(Guid messageId);
}

public class AdminService : IAdminService
{
    private readonly HttpClient _http;

    public AdminService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<SupportMessageDto>> GetSupportMessages(int count = 50) {
        return await _http.GetFromJsonAsync<List<SupportMessageDto>>($"api/Support/messages?count={count}") ?? new();
    }

    public async Task MarkSupportMessageRead(Guid messageId) {
        await _http.PostAsync($"api/Support/messages/{messageId}/read", null);
    }

    public async Task<AdminDashboardStats> GetDashboardStats()
    {
        return await _http.GetFromJsonAsync<AdminDashboardStats>("api/Admin/dashboard") ?? new();
    }

    public async Task<List<RtpTrendPoint>> GetRtpTrend()
    {
        return await _http.GetFromJsonAsync<List<RtpTrendPoint>>("api/Admin/analytics/rtp-trend") ?? new();
    }

    public async Task<ShadowCompareDto> GetShadowStats()
    {
        return await _http.GetFromJsonAsync<ShadowCompareDto>("api/Admin/analytics/shadow-mode") ?? new();
    }

    public async Task<List<ActiveSessionDto>> GetActiveSessions()
    {
        return await _http.GetFromJsonAsync<List<ActiveSessionDto>>("api/Admin/sessions/active") ?? new();
    }

    public async Task UpdateRtp(decimal targetRtp)
    {
        await _http.PostAsJsonAsync("api/Admin/config/rtp", new SetRtpDto { TargetRtp = targetRtp });
    }

    public async Task ToggleEmergencyStop(bool enabled)
    {
        await _http.PostAsJsonAsync("api/Admin/config/emergency-stop", new EmergencyStopDto { Enabled = enabled });
    }

    public async Task<IntegrityResponse> VerifyIntegrity()
    {
        return await _http.GetFromJsonAsync<IntegrityResponse>("api/Admin/audit/verify") 
               ?? new IntegrityResponse { IsValid = false, Message = "Failed to contact server" };
    }


    public async Task UpdateUserBalance(Guid userId, decimal newBalance) {
        await _http.PostAsJsonAsync($"api/Admin/players/{userId}/balance", new { NewBalance = newBalance });
    }
    public async Task UpdateVolatility(string mode) { await _http.PostAsJsonAsync("api/Admin/config/volatility", new { Mode = mode }); }
    public async Task ToggleUserStatus(Guid userId, bool isActive) {
        await _http.PostAsJsonAsync($"api/Admin/players/{userId}/status", new { IsActive = isActive });
    }
    public async Task<List<PlayerSearchResultDto>> SearchPlayers(string query) {
        return await _http.GetFromJsonAsync<List<PlayerSearchResultDto>>($"api/Admin/players/search/{query}") ?? new();
    }

    public async Task TriggerAction(string actionType) {
        await _http.PostAsJsonAsync("api/Admin/actions/trigger", new { ActionType = actionType });
    }

    public async Task<List<AuditLogDto>> GetAuditLogs() {
        return await _http.GetFromJsonAsync<List<AuditLogDto>>("api/Admin/audit-logs") ?? new();
    }

    public async Task<SimulationReport?> RunSimulation(SimulationRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/Admin/simulation/run", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<SimulationReport>();
        }
        return null;
    }
}

public class IntegrityResponse { public bool IsValid { get; set; } public string Message { get; set; } = ""; }
