using AleaSim.Api.Models;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace AleaSim.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase {
    private readonly IGameDirector _gameDirector;

    public GameController(IGameDirector gameDirector) {
        _gameDirector = gameDirector;
    }

    [HttpPost("{gameType}/session")]
    public async Task<IActionResult> StartSession(string gameType, [FromBody] StartSessionRequest request) {
        try {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            
            var session = await _gameDirector.StartSession(gameType, userId);
            
            return Ok(new StartSessionResponse(session.Id, session.GameId, session.StartedAt));
        }
        catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{gameType}/bet/{sessionId}")]
    public async Task<IActionResult> PlaceBet(string gameType, Guid sessionId, [FromBody] PlaceBetRequest request) {
        try {
            var round = await _gameDirector.PlayRound(gameType, sessionId, request.Amount, request.BetData);

            return Ok(new PlaceBetResponse(round.Id, round.TotalWinAmount, round.RandomResult, false)); 
        }
        catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{gameType}/action/{sessionId}")]
    public async Task<IActionResult> PerformAction(string gameType, Guid sessionId, [FromBody] GameActionRequest request) {
        try {
             var newState = await _gameDirector.ProcessAction(gameType, sessionId, request.Action, request.ActionData);
             
             return Ok(new GameActionResponse(true, "Action processed", newState));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}