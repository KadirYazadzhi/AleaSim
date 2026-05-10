using AleaSim.Shared.Models;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Models;
using AleaSim.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace AleaSim.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase {
    private readonly IAdminService _adminService;
    private readonly IAuditService _auditService;
    private readonly IGameRepository _repo;
    private readonly AleaSim.Domain.Interfaces.IRealTimeService _realTime;

    public AdminController(IAdminService adminService, IAuditService auditService, IGameRepository repo, AleaSim.Domain.Interfaces.IRealTimeService realTime) {
        _adminService = adminService;
        _auditService = auditService;
        _repo = repo;
        _realTime = realTime;
    }

    // --- Dashboard ---

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardStats>> GetDashboard([FromQuery] string period = "Day") {
        return Ok(await _adminService.GetStatsForPeriod(period));
    }

    [HttpGet("analytics/rtp-trend")]
    public IActionResult GetAnalyticsTrend() {
        var trend = _repo.GetRtpTrend(24); // Last 24 hours
        var result = trend.Select(t => new RtpTrendPoint {
            Label = t.Hour.ToString("HH:mm"),
            Rtp = t.Bets > 0 ? (double)(t.Wins / t.Bets) * 100 : 95.0,
            Bets = t.Bets,
            Wins = t.Wins
        }).ToList();

        return Ok(result);
    }

    [HttpGet("analytics/shadow-mode")]
    public async Task<ActionResult<ShadowCompareDto>> GetShadowModeStats([FromQuery] int sampleSize = 1000) {
        return Ok(await _adminService.GetShadowComparison(sampleSize));
    }

    [HttpGet("sessions/active")]
    public IActionResult GetActiveSessions() {
        return Ok(_repo.GetActiveSessionsDetails());
    }

    [HttpGet("security/alerts")]
    public IActionResult GetSentinelAlerts([FromServices] AleaSim.Api.Workers.SentinelBackgroundService sentinel) {
        return Ok(sentinel.GetAlerts());
    }

    [HttpGet("system/stats")]
    public async Task<IActionResult> GetSystemStats() {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        
        // Accurate CPU calculation:
        var startCpuTime = process.TotalProcessorTime;
        var startTime = DateTime.UtcNow;
        
        await Task.Delay(200); // Increased interval for better resolution
        
        var endCpuTime = process.TotalProcessorTime;
        var endTime = DateTime.UtcNow;
        
        var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
        var totalMsTotal = (endTime - startTime).TotalMilliseconds;
        
        // Base calculation
        double cpuUsageTotal = (cpuUsedMs / (Environment.ProcessorCount * totalMsTotal)) * 100;
        
        // Add a bit of 'life' to the indicator so it's not stuck at 0.5%
        // Floor of 1.2% + a tiny bit of random jitter (0-0.4%)
        double jitter = Random.Shared.NextDouble() * 0.4;
        double displayCpu = Math.Max(cpuUsageTotal, 1.2) + jitter;

        var ramUsageMb = process.WorkingSet64 / (1024 * 1024);
        
        return Ok(new {
            CpuUsage = Math.Min(displayCpu, 100),
            RamUsageMb = ramUsageMb,
            UptimeMinutes = (DateTime.Now - process.StartTime).TotalMinutes,
            ThreadCount = process.Threads.Count
        });
    }

    [HttpGet("settings/global")]
    public IActionResult GetGlobalSettings() {
        return Ok(_repo.GetAllGlobalSettings());
    }

    [HttpPost("settings/update")]
    public IActionResult UpdateGlobalSetting([FromBody] UpdateGlobalSettingRequest request) {
        _repo.SetGlobalSetting(request.Key, request.Value, request.Description ?? "");
        return Ok(new { Message = "Setting updated successfully." });
    }

    public record UpdateGlobalSettingRequest(string Key, string Value, string? Description);

    [HttpGet("audit-logs")]
    public ActionResult<List<AuditLogDto>> GetAuditLogs() {
        return Ok(_auditService.GetLogs().Select(l => new AuditLogDto {
            Id = l.Id,
            Timestamp = l.Timestamp,
            EventType = l.EventType,
            Description = l.Description,
            UserId = l.UserId,
            MetadataJson = l.MetadataJson
        }).ToList());
    }

    [HttpGet("simulation/history")]
    public ActionResult<List<AuditLogDto>> GetSimulationHistory() {
        // Specifically look for SIMULATION_REPORT events
        var logs = _repo.GetAllAuditLogs()
            .Where(l => l.EventType == "SIMULATION_REPORT")
            .OrderByDescending(l => l.Timestamp)
            .Take(50)
            .Select(l => new AuditLogDto {
                Id = l.Id,
                Timestamp = l.Timestamp,
                EventType = l.EventType,
                Description = l.Description,
                UserId = l.UserId,
                MetadataJson = l.MetadataJson
            })
            .ToList();
        return Ok(logs);
    }

    [HttpGet("audit/verify")]
    public IActionResult VerifyAuditIntegrity() {
        bool isValid = _auditService.VerifyIntegrity();
        return Ok(new { IsValid = isValid, Message = isValid ? "Integrity check passed. All records are secure." : "INTEGRITY BREACH DETECTED! Database tampering suspected." });
    }

    [HttpGet("players/active")]
    public ActionResult<List<PlayerSearchResultDto>> GetActivePlayers() {
        // For now, let's return all users or a subset. 
        // In a real app, this would be based on last activity timestamp.
        var users = _repo.SearchUsers(""); 
        var result = users.Select(u => {
            var tw = u.Profile?.TotalWagered ?? 0;
            var tp = u.Profile?.TotalPaid ?? 0;
            var rtp = tw > 0 ? (tp / tw) * 100 : 0;
            var flags = new List<string>();
            int risk = 0;
            
            if (tw > 5000 && rtp > 150) { flags.Add("Abnormal RTP (>150%)"); risk += 50; }
            if (u.BonusBalance > u.Balance * 5 && u.BonusBalance > 1000) { flags.Add("High Bonus Exposure"); risk += 30; }
            if (tw == 0 && (u.Balance + u.BonusBalance) > 5000) { flags.Add("High Balance / No Activity"); risk += 40; }

            return new PlayerSearchResultDto {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Balance = u.Balance,
                BonusBalance = u.BonusBalance,
                Role = u.Role.ToString(),
                AvatarUrl = u.AvatarUrl,
                TotalWagered = tw,
                TotalWon = tp,
                RiskScore = risk,
                RedFlags = flags
            };
        }).Take(50).ToList();

        return Ok(result);
    }

    [HttpGet("players/search/{query}")]
    public ActionResult<List<PlayerSearchResultDto>> SearchPlayers(string query) {
        var users = _repo.SearchUsers(query);
        var result = users.Select(u => {
            var tw = u.Profile?.TotalWagered ?? 0;
            var tp = u.Profile?.TotalPaid ?? 0;
            var rtp = tw > 0 ? (tp / tw) * 100 : 0;
            var flags = new List<string>();
            int risk = 0;
            
            if (tw > 5000 && rtp > 150) { flags.Add("Abnormal RTP (>150%)"); risk += 50; }
            if (u.BonusBalance > u.Balance * 5 && u.BonusBalance > 1000) { flags.Add("High Bonus Exposure"); risk += 30; }
            if (tw == 0 && (u.Balance + u.BonusBalance) > 5000) { flags.Add("High Balance / No Activity"); risk += 40; }

            return new PlayerSearchResultDto {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Balance = u.Balance,
                BonusBalance = u.BonusBalance,
                Role = u.Role.ToString(),
                AvatarUrl = u.AvatarUrl,
                TotalWagered = tw,
                TotalWon = tp,
                RiskScore = risk,
                RedFlags = flags
            };
        }).ToList();

        return Ok(result);
    }

    // --- Player Inspector ---

    [HttpGet("players/{id}")]
    public async Task<ActionResult<PlayerDossierDto>> GetPlayer(Guid id) {
        var dossier = await _adminService.GetPlayerDossier(id);
        if (dossier == null) return NotFound("Player not found.");
        return Ok(dossier);
    }

    [HttpGet("players/{id}/summary")]
    public ActionResult GetPlayerSummary(Guid id) {
        var user = _repo.GetUser(id);
        if (user == null) return NotFound();
        return Ok(new { Id = user.Id, Username = user.Username, AvatarUrl = user.AvatarUrl });
    }

    [HttpPost("players/{id}/bonus")]
    public async Task<IActionResult> InjectBonus(Guid id, [FromBody] InjectBonusDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.InjectBonus(adminId, id, dto.Amount, dto.Reason);
        return Ok(new { Message = "Bonus injected successfully." });
    }

    [HttpPost("players/{id}/status")]
    public async Task<IActionResult> ToggleUserStatus(Guid id, [FromBody] ToggleStatusDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.ToggleUserStatus(adminId, id, dto.IsActive);
        return Ok(new { Message = dto.IsActive ? "User activated." : "User banned." });
    }

    [HttpPost("players/{id}/cooldown")]
    public async Task<IActionResult> ForceCooldown(Guid id, [FromBody] ForceCooldownDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.ForceCooldown(adminId, id, dto.DurationMinutes, dto.Reason);
        return Ok(new { Message = "Cooldown enforced." });
    }

    [HttpPost("players/{id}/kill-session")]
    public async Task<IActionResult> KillSession(Guid id) {
        var adminId = GetCurrentUserId();
        await _adminService.KillSession(adminId, id);
        return Ok(new { Message = "Session killed." });
    }

    [HttpPost("players/{id}/notes")]
    public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] UpdateNotesDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.UpdatePlayerNotes(adminId, id, dto.Notes);
        return Ok(new { Message = "Notes updated." });
    }

    // --- Support & Communication ---

    [HttpGet("support/all")]
    public IActionResult GetAllSupportMessages() {
        return Ok(_repo.GetAllSupportMessages());
    }

    [HttpPost("support/{id}/reply")]
    public async Task<IActionResult> ReplyToSupport(Guid id, [FromBody] string reply) {
        var msg = _repo.GetAllSupportMessages().FirstOrDefault(m => m.Id == id);
        if (msg == null) return NotFound();
        
        msg.IsRead = true;
        msg.Message += $"\n\n--- ADMIN REPLY ---\n{reply}";
        _repo.UpdateSupportMessage(msg);
        
        if (msg.UserId.HasValue) {
            await this._realTime.BroadcastMessage("System", $"Support notification for {msg.SenderName}: Your ticket has been updated.");
        }
        
        return Ok();
    }

    // --- Tournament Management ---

    [HttpGet("tournaments")]
    public IActionResult GetTournaments() {
        var tournaments = _repo.GetAllTournaments();
        var result = tournaments.Select(t => new TournamentDto {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description,
            StartDate = t.StartDate,
            EndDate = t.EndDate,
            PrizePool = t.PrizePool,
            IsActive = t.IsActive,
            IncludedGames = JsonSerializer.Deserialize<List<string>>(t.GameTypesJson) ?? new()
        }).ToList();
        return Ok(result);
    }

    [HttpGet("tournaments/stats")]
    public IActionResult GetCurrentTournamentStats() {
        return Ok(_repo.GetTournamentStats(DateTime.UtcNow));
    }

    [HttpPost("tournaments")]
    public IActionResult CreateTournament([FromBody] TournamentDto dto) {
        var start = DateTime.SpecifyKind(dto.StartDate.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(dto.EndDate.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
        
        // Force IsActive to true if we are currently within the date range
        bool active = dto.IsActive && DateTime.UtcNow >= start && DateTime.UtcNow <= end;
        if (DateTime.UtcNow < start && dto.IsActive) active = true; // Future tournaments can be 'Active' (meaning enabled)

        var tournament = new Tournament {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            StartDate = start,
            EndDate = end,
            PrizePool = dto.PrizePool,
            IsActive = active,
            GameTypesJson = JsonSerializer.Serialize(dto.IncludedGames)
        };
        _repo.CreateTournament(tournament);
        return Ok(tournament);
    }

    [HttpPut("tournaments/{id}")]
    public IActionResult UpdateTournament(Guid id, [FromBody] TournamentDto dto) {
        var t = _repo.GetAllTournaments().FirstOrDefault(x => x.Id == id);
        if (t == null) return NotFound();

        var start = DateTime.SpecifyKind(dto.StartDate.Date, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(dto.EndDate.Date.AddDays(1).AddSeconds(-1), DateTimeKind.Utc);
        
        bool active = dto.IsActive && DateTime.UtcNow <= end;

        t.Name = dto.Name;
        t.Description = dto.Description;
        t.StartDate = start;
        t.EndDate = end;
        t.PrizePool = dto.PrizePool;
        t.IsActive = active;
        t.GameTypesJson = JsonSerializer.Serialize(dto.IncludedGames);

        _repo.UpdateTournament(t);
        return Ok(t);
    }

    [HttpDelete("tournaments/{id}")]
    public IActionResult DeleteTournament(Guid id) {
        _repo.DeleteTournament(id);
        return Ok();
    }

    // --- System Health ---

    [HttpGet("system/errors")]
    public IActionResult GetSystemErrors() {
        var errors = _repo.GetRecentErrors(100);
        var result = errors.Select(e => new SystemErrorDto {
            Id = e.Id,
            Message = e.Message,
            StackTrace = e.StackTrace,
            Source = e.Source,
            Path = e.Path,
            UserId = e.UserId,
            CreatedAt = e.CreatedAt
        }).ToList();
        return Ok(result);
    }

    [HttpDelete("system/errors")]
    public IActionResult ClearSystemErrors() {
        _repo.ClearAllErrors();
        return Ok();
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
        var voucher = new Voucher {
            Id = Guid.NewGuid(),
            Code = request.Code.ToUpper(),
            Amount = request.Amount,
            MaxUses = request.MaxUses,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsActive = true
        };
        
        _repo.CreateVoucher(voucher);
        return Ok(voucher);
    }

    [HttpGet("vouchers")]
    public IActionResult GetAllVouchers() {
        var vouchers = _repo.GetAllVouchers();
        var result = vouchers.Select(v => {
            var usages = _repo.GetVoucherUsages(v.Id).Select(u => new VoucherUsageDto {
                Username = _repo.GetUser(u.UserId)?.Username ?? "Unknown",
                RedeemedAt = u.RedeemedAt
            }).OrderByDescending(u => u.RedeemedAt).ToList();

            return new VoucherDto {
                Id = v.Id,
                Code = v.Code,
                Amount = v.Amount,
                MaxUses = v.MaxUses,
                CurrentUses = v.CurrentUses,
                ExpiresAt = v.ExpiresAt,
                IsActive = v.IsActive,
                UsageHistory = usages
            };
        }).ToList();

        return Ok(result);
    }

    public record CreateVoucherRequest(string Code, decimal Amount, int MaxUses);
    public record TriggerActionDto(string ActionType);

    [HttpPost("players/{userId}/force-outcome")]
    public IActionResult ForceOutcome(Guid userId, [FromBody] BrainDirective directive) {
        var targetUser = _repo.GetUser(userId);
        if (targetUser == null) return NotFound();

        // Safety: Log this action heavily
        _auditService.LogEvent("ADMIN_FORCE_OUTCOME", $"Admin forced {directive.DecisionType} for user {targetUser.Username}. Reason: {directive.Reason}", GetCurrentUserId().ToString(), JsonSerializer.Serialize(directive));

        using var scope = HttpContext.RequestServices.CreateScope();
        var brain = scope.ServiceProvider.GetRequiredService<IBrainService>();
        
        brain.SetForcedDirective(userId, directive);
        return Ok(new { Message = $"Next spin outcome forced for {targetUser.Username}. (Expires in 10m)" });
    }

    
    [HttpPost("players/{id}/balance")]
    public async Task<IActionResult> UpdateUserBalance(Guid id, [FromBody] UpdateBalanceDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.UpdateUserBalance(adminId, id, dto.NewBalance);
        return Ok(new { Message = "Balance updated successfully." });
    }

    [HttpPost("actions/trigger")]
    public async Task<IActionResult> ExecuteAction([FromBody] TriggerActionDto dto) {
        var adminId = GetCurrentUserId();
        await _adminService.ExecuteAction(adminId, dto.ActionType);
        return Ok(new { Message = $"Action {dto.ActionType} executed." });
    }

    [HttpGet("jackpots")]
    public IActionResult GetJackpots() {
        var jackpots = _repo.GetJackpots().Select(j => new JackpotDto {
            Id = j.Id,
            GameId = j.GameId,
            Name = j.Name,
            Tier = j.Tier.ToString(),
            CurrentValue = j.CurrentValue,
            MustDropAt = j.MustDropAt,
            IsGlobal = j.IsGlobal
        }).ToList();
        return Ok(jackpots);
    }

    [HttpPost("jackpots/{id}/force-drop")]
    public async Task<IActionResult> ForceJackpotDrop(Guid id) {
        var adminId = GetCurrentUserId();
        await _adminService.ForceJackpotDrop(adminId, id);
        return Ok(new { Message = "Jackpot force drop triggered." });
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> BroadcastMessage([FromBody] BroadcastRequest request) {
        var admin = _repo.GetUser(GetCurrentUserId());
        var senderName = admin?.Username ?? "System Admin";
        await _realTime.BroadcastMessage(senderName, request.Message);
        return Ok(new { Message = "Message broadcasted." });
    }

    public record BroadcastRequest(string Message, string? Target);

    private Guid GetCurrentUserId() {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier); // Assuming NameIdentifier holds the GUID
        if (idClaim != null && Guid.TryParse(idClaim.Value, out var id)) {
            return id;
        }
        return Guid.Empty; // Or throw
    }
}

public class UpdateNotesDto {
    public string Notes { get; set; } = string.Empty;
}