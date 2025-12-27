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
    private readonly IGameRepository _repository;

    public AdminController(IAuditService auditService, IJackpotService jackpotService, IRtpEngine rtpEngine, IGameRepository repository) {
        _auditService = auditService;
        _jackpotService = jackpotService;
        _rtpEngine = rtpEngine;
        _repository = repository;
    }

    [HttpGet("audit-logs")]
    public IActionResult GetAuditLogs() {
        return Ok(_auditService.GetLogs()); // Audit service manages its own locking/access usually or we need to update it too
    }

    [HttpGet("jackpot/global")]
    public IActionResult GetGlobalJackpot() {
        return Ok(_jackpotService.GetGlobalJackpot(_repository));
    }

    [HttpGet("rtp/game/{gameId}")]
    public IActionResult GetGameRtp(Guid gameId) {
        return Ok(_rtpEngine.GetGameStats(gameId, _repository));
    }
}
