using AleaSim.Shared.Models;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using AleaSim.Domain.Entities;

namespace AleaSim.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GameController : ControllerBase {
    private readonly IGameDirector _gameDirector;
    private readonly IVaultService _vaultService;
    private readonly IGameRepository _repo;
    private readonly AleaSimDbContext _context;
    private readonly ITournamentService _tournamentService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly IPromotionService _promotionService;
    private readonly IVoucherService _voucherService;
    private readonly ILevelService _levelService;
    private readonly IQuestService _questService;
    private readonly IRedisCacheService _redisCache;
    private readonly ILogger<GameController> _logger;
    private readonly IRealTimeService _realTime;

    public GameController(
        IGameDirector gameDirector, 
        IVaultService vaultService, 
        IGameRepository repo,
        AleaSimDbContext context,
        ITournamentService tournamentService,
        ILeaderboardService leaderboardService,
        IPromotionService promotionService,
        IVoucherService voucherService,
        ILevelService levelService,
        IQuestService questService,
        IRedisCacheService redisCache,
        IRealTimeService realTime,
        ILogger<GameController> logger) 
    {
        _gameDirector = gameDirector;
        _vaultService = vaultService;
        _repo = repo;
        _context = context;
        _tournamentService = tournamentService;
        _leaderboardService = leaderboardService;
        _promotionService = promotionService;
        _voucherService = voucherService;
        _levelService = levelService;
        _questService = questService;
        _redisCache = redisCache;
        _realTime = realTime;
        _logger = logger;
    }

    [HttpPost("bonus/cashout")]
    public async Task<IActionResult> CashoutBonus() {
        try {
            var userId = GetUserIdOrThrow();
            bool result = await _vaultService.CashoutBonusAsync(userId, _repo);
            
            if (result) return Ok("Bonus processed (Cashed out or Forfeited).");
            return BadRequest("No active bonus to cash out.");
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in CashoutBonus");
            return StatusCode(500, "Internal Server Error");
        }
    }

    [HttpGet("{gameType}/session")]
    public async Task<IActionResult> ResumeSession(string gameType) {
        try {
            var userId = GetUserIdOrThrow();
            var game = _repo.GetGameByType(gameType);
            if (game == null) return NotFound("Game type not found");

            // Try to find session ID in Redis first
            string sessionCacheKey = $"active_session:{userId}:{game.Id}";
            var sessionIdStr = await _redisCache.GetAsync<string>(sessionCacheKey);
            
            GameSession? session = null;
            if (!string.IsNullOrEmpty(sessionIdStr) && Guid.TryParse(sessionIdStr, out var sessionId)) {
                session = _repo.GetSession(sessionId);
            }

            if (session == null) {
                session = _repo.GetAllActiveSessions()
                    .FirstOrDefault(s => s.UserId == userId && s.GameId == game.Id);
                
                if (session != null) {
                    await _redisCache.SetAsync(sessionCacheKey, session.Id.ToString(), TimeSpan.FromHours(2));
                }
            }

            if (session == null) return NoContent();

            var state = await _gameDirector.GetCurrentState(gameType, session.Id);

            return Ok(new {
                SessionId = session.Id,
                GameId = session.GameId,
                StartedAt = session.StartedAt,
                GameState = state
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in ResumeSession");
            return StatusCode(500, "Internal Server Error");
        }
    }

    [HttpPost("{gameType}/session")]
    public async Task<IActionResult> StartSession(string gameType, [FromBody] StartSessionRequest request) {
        try {
            var userId = GetUserIdOrThrow();
            var session = await _gameDirector.StartSession(gameType, userId, request.ClientSeed);
            return Ok(new StartSessionResponse(session.Id, session.GameId, session.StartedAt, session.ClientSeed, session.ServerSeedHash));
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in StartSession");
            return BadRequest(ex.Message); // Keeping message for game logic errors (e.g. invalid game type)
        }
    }

    [HttpPost("{gameType}/bet/{sessionId}")]
    public async Task<IActionResult> PlaceBet(string gameType, Guid sessionId, [FromBody] PlaceBetRequest request) {
        try {
            if (request.Amount < 0) return BadRequest("Bet amount cannot be negative.");

            var userId = GetUserIdOrThrow();

            // BOT PROTECTION: Rate Limiting (Max 5 bets per second)
            string rateLimitKey = $"ratelimit:bet:{userId}";
            bool isLimited = await _redisCache.IncrementRateLimitAsync(rateLimitKey, TimeSpan.FromSeconds(1));
            if (isLimited) return StatusCode(429, "Too many requests. Slow down, partner!");

            var round = await _gameDirector.PlayRound(gameType, userId, sessionId, request.Amount, request.BetData);

            // Update Quests
            _ = _questService.UpdateProgressAsync(userId, "SpinCount", 1, _repo, _realTime, _vaultService);
            _ = _questService.UpdateProgressAsync(userId, "TotalWager", request.Amount, _repo, _realTime, _vaultService);
            if (round.TotalWinAmount > 0) {
                _ = _questService.UpdateProgressAsync(userId, "WinAmount", round.TotalWinAmount, _repo, _realTime, _vaultService);
            }

            var session = _repo.GetSession(sessionId);
            var profile = session != null ? _repo.GetPlayerProfile(session.UserId) : null;

            float flowIntensity = 0f;
            if (profile != null) {
                if (profile.AvgSpinInterval < 2.5) flowIntensity = 1.0f; // High Flow
                else if (profile.AvgSpinInterval < 4.0) flowIntensity = 0.6f; // Medium Flow
                else if (profile.AvgSpinInterval < 6.0) flowIntensity = 0.3f; // Warming up
            }

            bool isNearMiss = round.DecisionType == "CoolDown";

            return Ok(new PlaceBetResponse(round.Id, round.TotalWinAmount, round.RandomResult, false) {
                FlowStateIntensity = flowIntensity,
                IsNearMiss = isNearMiss
            }); 
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in PlaceBet");
            if (ex.Message.Contains("Insufficient", StringComparison.OrdinalIgnoreCase)) {
                return StatusCode(402, ex.Message);
            }
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{gameType}/action/{sessionId}")]
    public async Task<IActionResult> PerformAction(string gameType, Guid sessionId, [FromBody] GameActionRequest request) {
        try {
             var userId = GetUserIdOrThrow();
             var newState = await _gameDirector.ProcessAction(gameType, userId, sessionId, request.Action, request.ActionData);
             string stateJson = JsonSerializer.Serialize(newState);
             return Ok(new GameActionResponse(true, "Action processed", stateJson));
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in PerformAction");
            if (ex.Message.Contains("Insufficient", StringComparison.OrdinalIgnoreCase)) {
                return StatusCode(402, ex.Message);
            }
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("leaderboard/{name}")]
    public async Task<IActionResult> GetLeaderboard(string name) {
        if (name.ToLower() == "tournament") {
            return Ok(await _tournamentService.GetCurrentRankings(_repo));
        }
        return Ok(_leaderboardService.GetLeaderboard(name));
    }

    [HttpGet("leaderboard/history")]
    public IActionResult GetTournamentHistory() {
        var history = _repo.GetTournamentHistory(6); // Last 6 months
        var result = history.Select(h => new TournamentHistoryDto {
            MonthName = h.Month.ToString("MMMM yyyy"),
            WinnerName = h.Username,
            AvatarUrl = h.AvatarUrl,
            Prize = h.PrizeAmount,
            Multiplier = h.Score
        });
        return Ok(result);
    }

    [HttpGet("quests")]
    public async Task<IActionResult> GetQuests() {
        var userId = GetUserIdOrThrow();
        var quests = await _questService.GetActiveQuests(userId, _repo);
        return Ok(quests.Select(p => new {
            Id = p.Quest.Id,
            p.Quest.Title,
            p.Quest.Description,
            p.CurrentValue,
            p.Quest.TargetValue,
            p.IsCompleted,
            p.Quest.RewardAmount,
            Percentage = (double)(p.CurrentValue / p.Quest.TargetValue * 100)
        }));
    }

    [AllowAnonymous]
    [HttpGet("public-history")]
    public IActionResult GetPublicHistory() {
        var result = _repo.GetGlobalHistory(100);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("audit-chain")]
    public IActionResult GetAuditChain() {
        var logs = _repo.GetAuditLogs(50);
        var result = logs.Select(l => new AuditLogDto {
            Id = l.Id,
            Timestamp = l.Timestamp,
            EventType = l.EventType,
            Description = l.Description,
            UserId = l.UserId,
            MetadataJson = l.MetadataJson,
            Hash = l.Hash,
            PreviousHash = l.PreviousHash
        });
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("jackpots")]
    public IActionResult GetJackpots() {
        var jackpots = _repo.GetJackpots();
        var result = jackpots.Select(j => new JackpotDto {
            Name = j.Name,
            CurrentValue = j.CurrentValue,
            MustDropAt = j.MustDropAt,
            IsGlobal = j.IsGlobal,
            Tier = j.Tier.ToString()
        }).ToList();
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("platform-stats")]
    public IActionResult GetPlatformStats() {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        
        // 1. Monthly wagering for Tournament Pool
        var monthlyBets = _context.Bets
            .Where(b => b.CreatedAt >= startOfMonth)
            .Join(_context.Users, b => b.UserId, u => u.Id, (b, u) => new { b, u })
            .Where(x => !x.u.Username.StartsWith("Sim_") && x.u.Role != AleaSim.Domain.Enums.Role.Admin)
            .Sum(x => (decimal?)x.b.Amount) ?? 0m;

        var activeCount = _repo.GetActivePlayerCount(10);
        
        var jackpots = _repo.GetJackpots();
        var spades = jackpots.FirstOrDefault(j => j.Tier == AleaSim.Domain.Entities.JackpotTier.Spades);
        
        decimal weeklyJackpot = spades?.CurrentValue ?? 0m;
        decimal totalRewards = _repo.GetGlobalTotalRewardsPaid();
        
        var tournamentEndsAt = startOfMonth.AddMonths(1).AddSeconds(-1);
        
        // Tournament pool: Base $25,000 + 1% of MONTHLY wagering volume
        decimal tournamentPool = 25000m + (monthlyBets * 0.01m); 

        var stats = new PlatformStatsDto {
            ActivePlayers = activeCount, 
            AverageRtp = 96.5, // Logic for real average RTP could be complex, keeping static or calc
            TotalRewardsPaid = totalRewards,
            WeeklyJackpot = weeklyJackpot,
            TournamentPrizePool = tournamentPool,
            TournamentEndsAt = tournamentEndsAt
        };

        return Ok(stats);
    }

    [HttpGet("history")]
    public IActionResult GetHistory() {
        var userId = GetUserIdOrThrow();
        // Uses the optimized N+1 fix
        var result = _repo.GetUserHistory(userId, 50);
        return Ok(result);
    }

    [HttpPost("daily-spin")]
    public async Task<IActionResult> DailySpin() {
        try {
            var userId = GetUserIdOrThrow();
            var result = await _promotionService.SpinBonusWheel(userId, _repo);
            return Ok(result);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in DailySpin");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("streak/claim")]
    public async Task<IActionResult> ClaimStreakReward() {
        try {
            var userId = GetUserIdOrThrow();
            var result = await _promotionService.ClaimDailyStreakReward(userId, _repo);
            return Ok(result);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in ClaimStreakReward");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("vouchers/redeem/{code}")]
    public async Task<IActionResult> RedeemVoucher(string code) {
        try {
            var userId = GetUserIdOrThrow();
            decimal amount = await _voucherService.RedeemVoucher(userId, code, _repo, _vaultService);
            return Ok(new { Message = "Voucher redeemed successfully!", Amount = amount });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in RedeemVoucher");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("skills/upgrade/{name}")]
    public async Task<IActionResult> UpgradeSkill(string name) {
        try {
            var userId = GetUserIdOrThrow();
            bool success = await _levelService.UpgradeSkill(userId, name, _repo);
            if (success) return Ok(new { Message = $"Skill {name} upgraded!" });
            return BadRequest("Insufficient skill points or invalid skill.");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error in UpgradeSkill");
            return BadRequest(ex.Message);
        }
    }

    private Guid GetUserIdOrThrow() {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !Guid.TryParse(idClaim.Value, out var id)) {
            throw new UnauthorizedAccessException("Invalid User Token");
        }
        
        // Ensure user still exists in DB (e.g. after DB reset)
        if (_repo.GetUser(id) == null) {
            throw new UnauthorizedAccessException("User no longer exists");
        }

        return id;
    }
}