using AleaSim.Shared.Models; // Changed from AleaSim.Api.Models
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
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

    [HttpGet("audit-logs")]
    public IActionResult GetAuditLogs() {
        return Ok(_auditService.GetLogs());
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

    private Guid GetCurrentUserId() {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier); // Assuming NameIdentifier holds the GUID
        if (idClaim != null && Guid.TryParse(idClaim.Value, out var id)) {
            return id;
        }
        return Guid.Empty; // Or throw
    }
}
