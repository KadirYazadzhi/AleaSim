using AleaSim.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AleaSim.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase {
    private readonly IAuditService _auditService;
    private readonly IJackpotService _jackpotService;
    private readonly IRtpEngine _rtpEngine;

    public AdminController(IAuditService auditService, IJackpotService jackpotService, IRtpEngine rtpEngine) {
        _auditService = auditService;
        _jackpotService = jackpotService;
        _rtpEngine = rtpEngine;
    }

    [HttpGet("audit-logs")]
    public IActionResult GetAuditLogs() {
        return Ok(_auditService.GetLogs());
    }

    [HttpGet("jackpot/global")]
    public IActionResult GetGlobalJackpot() {
        return Ok(_jackpotService.GetGlobalJackpot());
    }

    [HttpGet("rtp/game/{gameId}")]
    public IActionResult GetGameRtp(Guid gameId) {
        return Ok(_rtpEngine.GetGameStats(gameId));
    }
}
