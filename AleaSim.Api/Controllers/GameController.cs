using AleaSim.Api.Models;
using AleaSim.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace AleaSim.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase {
    private readonly Func<string, IGame> _gameResolver;
    private readonly IAuditService _auditService;

    public GameController(Func<string, IGame> gameResolver, IAuditService auditService) {
        _gameResolver = gameResolver;
        _auditService = auditService;
    }

    [HttpPost("{gameType}/session")]
    public IActionResult StartSession(string gameType, [FromBody] StartSessionRequest request) {
        try {
            var game = _gameResolver(gameType);
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            
            var session = game.StartSession(userId);
            
            _auditService.LogEvent("SESSION_START", $"Started {gameType} session", userId.ToString(), JsonSerializer.Serialize(new { session.Id }));

            return Ok(new StartSessionResponse(session.Id, session.GameId, session.StartedAt));
        }
        catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{gameType}/bet/{sessionId}")]
    public IActionResult PlaceBet(string gameType, Guid sessionId, [FromBody] PlaceBetRequest request) {
        try {
            var game = _gameResolver(gameType);
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

            string betDataString = JsonSerializer.Serialize(request.BetData);
            
            game.PlaceBet(sessionId, request.Amount, betDataString);
            var round = game.ResolveRound(sessionId);
            
            _auditService.LogEvent("BET_PLACED", $"Bet {request.Amount} on {gameType}", userId, 
                JsonSerializer.Serialize(new { RoundId = round.Id, Win = round.TotalWinAmount }));

            return Ok(new PlaceBetResponse(round.Id, round.TotalWinAmount, round.RandomResult, false)); // Jackpot flag mocked
        }
        catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{gameType}/action/{sessionId}")]
    public IActionResult PerformAction(string gameType, Guid sessionId, [FromBody] GameActionRequest request) {
        try {
            var game = _gameResolver(gameType);
             game.ProcessAction(sessionId, request.Action, request.ActionData);
             
             var newState = game.GetCurrentState(sessionId);
             
             return Ok(new GameActionResponse(true, "Action processed", newState));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
