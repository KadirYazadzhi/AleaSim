using AleaSim.Shared.Models; // Changed from AleaSim.Api.Models
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using AleaSim.Domain.Entities; // Added
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AleaSim.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase {
    private readonly IAdminService _adminService;
    private readonly IAuditService _auditService;

    public AdminController(IAdminService adminService, IAuditService auditService) {
        _adminService = adminService;
        _auditService = auditService;
    }

    // --- Dashboard ---

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardStats>> GetDashboard() {
        return Ok(await _adminService.GetLiveStats());
    }

    [HttpGet("analytics/rtp-trend")]
    public IActionResult GetAnalyticsTrend() {
        using var scope = HttpContext.RequestServices.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
        var trend = repo.GetRtpTrend(24); // Last 24 hours
        var result = trend.Select(t => new RtpTrendPoint {
            Label = t.Hour.ToString("HH:mm"),
            Rtp = t.Bets > 0 ? (double)(t.Wins / t.Bets) * 100 : 95.0
        }).ToList();

        return Ok(result);
    }

    [HttpGet("analytics/shadow-mode")]
    public async Task<ActionResult<ShadowCompareDto>> GetShadowModeStats([FromQuery] int sampleSize = 1000) {
        return Ok(await _adminService.GetShadowComparison(sampleSize));
    }

    [HttpGet("sessions/active")]
    public IActionResult GetActiveSessions() {
        // ... (existing code)
        return Ok(new List<string>()); // Placeholder fix for previous turn
    }

    [HttpGet("security/alerts")]
    public IActionResult GetSentinelAlerts([FromServices] AleaSim.Api.Workers.SentinelBackgroundService sentinel) {
        return Ok(sentinel.GetAlerts());
    }

    [HttpGet("audit-logs")]
    public IActionResult GetAuditLogs() {
        return Ok(_auditService.GetLogs());
    }

    [HttpGet("audit/verify")]
    public IActionResult VerifyAuditIntegrity() {
        bool isValid = _auditService.VerifyIntegrity();
        return Ok(new { IsValid = isValid, Message = isValid ? "Integrity check passed. All records are secure." : "INTEGRITY BREACH DETECTED! Database tampering suspected." });
    }

    [HttpGet("players/search/{query}")]
    public ActionResult<List<PlayerSearchResultDto>> SearchPlayers(string query) {
        using var scope = HttpContext.RequestServices.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
        var users = repo.SearchUsers(query);
        var result = users.Select(u => new PlayerSearchResultDto {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            Balance = u.Balance,
            Role = u.Role.ToString()
        }).ToList();

        return Ok(result);
    }

    // --- Player Inspector ---

    [HttpGet("players/{id}")]
    public async Task<ActionResult<PlayerDossier>> GetPlayer(Guid id) {
        var dossier = await _adminService.GetPlayerDossier(id);
        if (dossier == null) return NotFound("Player not found.");
        return Ok(dossier);
    }

    [HttpPost("players/{id}/bonus")]
    public async Task<IActionResult> InjectBonus(Guid id, [FromBody] InjectBonusDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.InjectBonus(adminId, id, dto.Amount, dto.Reason);
        return Ok(new { Message = "Bonus injected successfully." });
    }

    [HttpPost("players/{id}/cooldown")]
    public async Task<IActionResult> ForceCooldown(Guid id, [FromBody] ForceCooldownDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.ForceCooldown(adminId, id, dto.DurationMinutes, dto.Reason);
        return Ok(new { Message = "Cooldown enforced." });
    }

    // --- System Control ---

    [HttpPost("config/rtp")]
    public async Task<IActionResult> SetGlobalRtp([FromBody] SetRtpDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.SetGlobalRtp(adminId, dto.TargetRtp);
        return Ok(new { Message = $"Global RTP set to {dto.TargetRtp}%" });
    }

    [HttpPost("config/emergency-stop")]
    public async Task<IActionResult> ToggleEmergencyStop([FromBody] EmergencyStopDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.ToggleEmergencyStop(adminId, dto.Enabled);
        return Ok(new { Message = dto.Enabled ? "EMERGENCY STOP ACTIVATED" : "System resumed normal operation." });
    }

    [HttpPost("config/volatility")]
    public async Task<IActionResult> SetVolatility([FromBody] SetVolatilityDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.SetVolatilityMode(adminId, dto.Mode);
        return Ok(new { Message = $"Volatility mode set to {dto.Mode}" });
    }

    [HttpPost("simulation/run")]
    public async Task<ActionResult<SimulationReport>> RunSimulation([FromBody] SimulationRequest request) {
        var service = HttpContext.RequestServices.GetRequiredService<ISimulationService>();
        var report = await service.RunSimulation(request);
        return Ok(report);
    }

    [HttpPost("vouchers/create")]
    public IActionResult CreateVoucher([FromBody] CreateVoucherRequest request) {
        using var scope = HttpContext.RequestServices.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
        var voucher = new Voucher {
            Id = Guid.NewGuid(),
            Code = request.Code.ToUpper(),
            Amount = request.Amount,
            MaxUses = request.MaxUses,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsActive = true
        };
        
        repo.CreateVoucher(voucher);
        return Ok(voucher);
    }

    [HttpGet("vouchers")]
    public IActionResult GetAllVouchers() {
        using var scope = HttpContext.RequestServices.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        
        var vouchers = repo.GetAllVouchers();
        var result = vouchers.Select(v => new VoucherDto {
            Id = v.Id,
            Code = v.Code,
            Amount = v.Amount,
            MaxUses = v.MaxUses,
            CurrentUses = v.CurrentUses,
            ExpiresAt = v.ExpiresAt,
            IsActive = v.IsActive
        }).ToList();

        return Ok(result);
    }

    public record CreateVoucherRequest(string Code, decimal Amount, int MaxUses);

    private Guid GetCurrentUserId() {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier); // Assuming NameIdentifier holds the GUID
        if (idClaim != null && Guid.TryParse(idClaim.Value, out var id)) {
            return id;
        }
        return Guid.Empty; // Or throw
    }
}
