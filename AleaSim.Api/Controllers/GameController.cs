using AleaSim.Shared.Models; // Changed
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase {
    private readonly IGameDirector _gameDirector;
    private readonly IVaultService _vaultService;
    private readonly IServiceScopeFactory _scopeFactory;

    public GameController(IGameDirector gameDirector, IVaultService vaultService, IServiceScopeFactory scopeFactory) {
        _gameDirector = gameDirector;
        _vaultService = vaultService;
        _scopeFactory = scopeFactory;
    }

    [HttpPost("bonus/cashout")]
    public IActionResult CashoutBonus() {
        try {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
            
            bool result = _vaultService.CashoutBonus(userId, repo);
            
            if (result) return Ok("Bonus processed (Cashed out or Forfeited).");
            return BadRequest("No active bonus to cash out.");
        }
        catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{gameType}/session")]
    public async Task<IActionResult> StartSession(string gameType, [FromBody] StartSessionRequest request) {
        try {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
            
            var session = await _gameDirector.StartSession(gameType, userId, request.ClientSeed);
            
            return Ok(new StartSessionResponse(session.Id, session.GameId, session.StartedAt, session.ClientSeed, session.ServerSeedHash));
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
             
             // Serialize object to string for DTO
             string stateJson = JsonSerializer.Serialize(newState);
             return Ok(new GameActionResponse(true, "Action processed", stateJson));
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

        [HttpGet("leaderboard/{name}")]

        public IActionResult GetLeaderboard(string name) {

            var service = HttpContext.RequestServices.GetService<ILeaderboardService>();

            if (service == null) return BadRequest("Leaderboard service unavailable");

            return Ok(service.GetLeaderboard(name));

        }

    

                [HttpGet("quests")]

    

                public IActionResult GetQuests() {

    

                    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    

                    using var scope = _scopeFactory.CreateScope();

    

                    var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

    

                    return Ok(repo.GetActiveQuests(userId));

    

                }

    

            

    

                [HttpGet("history")]

    

                public IActionResult GetHistory() {

    

                    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    

                    using var scope = _scopeFactory.CreateScope();

    

                    var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

    

                    

    

                    var rounds = repo.GetUserRounds(userId, 50);

    

                    var result = rounds.Select(r => {

    

                        // Need GameName. Usually we'd join but for now we infer from session

    

                        var session = repo.GetSession(r.GameSessionId);

    

                        var game = session != null ? repo.GetGame(session.GameId) : null;

    

                        

    

                        return new GameRoundDto {

    

                            Id = r.Id,

    

                            GameName = game?.Name ?? "Unknown Game",

    

                            BetAmount = r.TotalBetAmount,

    

                            WinAmount = r.TotalWinAmount,

    

                            PlayedAt = r.ExecutedAt,

    

                            ResultSummary = r.DecisionType // Use DecisionType as a summary

    

                        };

    

                    });

    

            

    

                    return Ok(result);

    

                }

    

            

    

        

    

                [HttpPost("daily-spin")]

    

        

    

                public async Task<IActionResult> DailySpin() {

    

        

    

                    try {

    

        

    

                        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    

        

    

                        using var scope = _scopeFactory.CreateScope();

    

        

    

                        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

    

        

    

                        var promo = scope.ServiceProvider.GetRequiredService<IPromotionService>();

    

        

    

                        

    

        

    

                        var result = await promo.SpinBonusWheel(userId, repo);

    

        

    

                        return Ok(result);

    

        

    

                    }

    

        

    

                    catch (Exception ex) {

    

        

    

                        return BadRequest(ex.Message);

    

        

    

                    }

    

        

    

                }

    

        

    

            

    

        

    

                    [HttpPost("vouchers/redeem/{code}")]

    

        

    

            

    

        

    

                    public async Task<IActionResult> RedeemVoucher(string code) {

    

        

    

            

    

        

    

                        try {

    

        

    

            

    

        

    

                            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    

        

    

            

    

        

    

                            using var scope = _scopeFactory.CreateScope();

    

        

    

            

    

        

    

                            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

    

        

    

            

    

        

    

                            var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();

    

        

    

            

    

        

    

                            var voucherService = scope.ServiceProvider.GetRequiredService<IVoucherService>();

    

        

    

            

    

        

    

                            

    

        

    

            

    

        

    

                            decimal amount = await voucherService.RedeemVoucher(userId, code, repo, vault);

    

        

    

            

    

        

    

                            return Ok(new { Message = "Voucher redeemed successfully!", Amount = amount });

    

        

    

            

    

        

    

                        }

    

        

    

            

    

        

    

                        catch (Exception ex) {

    

        

    

            

    

        

    

                            return BadRequest(ex.Message);

    

        

    

            

    

        

    

                        }

    

        

    

            

    

        

    

                    }

    

        

    

            

    

        

    

                

    

        

    

            

    

        

    

                    [HttpPost("skills/upgrade/{name}")]

    

        

    

            

    

        

    

                    public async Task<IActionResult> UpgradeSkill(string name) {

    

        

    

            

    

        

    

                        try {

    

        

    

            

    

        

    

                            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());

    

        

    

            

    

        

    

                            using var scope = _scopeFactory.CreateScope();

    

        

    

            

    

        

    

                            var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();

    

        

    

            

    

        

    

                            var levelService = scope.ServiceProvider.GetRequiredService<ILevelService>();

    

        

    

            

    

        

    

                            

    

        

    

            

    

        

    

                            bool success = await levelService.UpgradeSkill(userId, name, repo);

    

        

    

            

    

        

    

                            if (success) return Ok(new { Message = $"Skill {name} upgraded!" });

    

        

    

            

    

        

    

                            return BadRequest("Insufficient skill points or invalid skill.");

    

        

    

            

    

        

    

                        }

    

        

    

            

    

        

    

                        catch (Exception ex) {

    

        

    

            

    

        

    

                            return BadRequest(ex.Message);

    

        

    

            

    

        

    

                        }

    

        

    

            

    

        

    

                    }

    

        

    

            

    

        

    

                }

    

        

    

            

    

        

    

                

    

        

    

            

    

        

    