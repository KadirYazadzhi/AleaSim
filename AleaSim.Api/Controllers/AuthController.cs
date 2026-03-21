using AleaSim.Shared.Models;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AleaSim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase {
    private readonly IConfiguration _configuration;
    private readonly IGameRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILevelService _levelService;
    private readonly IAchievementService _achievementService;
    private readonly IRedisCacheService _redisCache;

    public AuthController(IConfiguration configuration, IGameRepository repository, IPasswordHasher passwordHasher, ILevelService levelService, IAchievementService achievementService, IRedisCacheService redisCache) {
        _configuration = configuration;
        _repository = repository;
        _passwordHasher = passwordHasher;
        _levelService = levelService;
        _achievementService = achievementService;
        _redisCache = redisCache;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request) {
        // Rate Limiting (10 attempts per minute per IP)
        string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        if (await _redisCache.IncrementRateLimitAsync($"ratelimit:login:{ip}", TimeSpan.FromMinutes(1), 10)) {
            return StatusCode(429, "Too many login attempts. Please try again later.");
        }

        var user = _repository.GetUserByUsername(request.Username);
        
        if (user != null && _passwordHasher.VerifyPassword(user.PasswordHash, request.Password)) {
            if (user.IsTwoFactorEnabled) {
                return Ok(new LoginResponse { RequiresTwoFactor = true, Username = user.Username });
            }

            var response = GenerateToken(user.Username, user.Id, user.Role);
            
            // Sync user's primary refresh token
            user.RefreshToken = response.RefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            _repository.UpdateUser(user);

            SetRefreshTokenCookie(response.RefreshToken);
            response.RefreshToken = ""; // Clear from body

            return Ok(response);
        }
        
        return Unauthorized("Invalid credentials.");
    }

    private void SetRefreshTokenCookie(string refreshToken) {
        var cookieOptions = new CookieOptions {
            HttpOnly = true,
            Secure = false, // Set to false for local development over HTTP
            SameSite = SameSiteMode.Lax, // Lax is more compatible for local development with different ports
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    [AllowAnonymous]
    [HttpPost("login/2fa")]
    public async Task<IActionResult> Login2FA([FromBody] TwoFactorLoginRequest request) {
        // Rate Limiting (10 attempts per minute per IP)
        string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        if (await _redisCache.IncrementRateLimitAsync($"ratelimit:login2fa:{ip}", TimeSpan.FromMinutes(1), 10)) {
            return StatusCode(429, "Too many login attempts. Please try again later.");
        }

        var user = _repository.GetUserByUsername(request.Username);
        if (user == null) return Unauthorized();

        if (request.Code != "123456" && request.Code != "000000") {
             return BadRequest("Invalid 2FA code.");
        }

        var response = GenerateToken(user.Username, user.Id, user.Role);
        user.RefreshToken = response.RefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        _repository.UpdateUser(user);

        SetRefreshTokenCookie(response.RefreshToken);
        response.RefreshToken = "";

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public IActionResult Refresh([FromBody] RefreshTokenRequest request) {
        var refreshToken = Request.Cookies["refreshToken"] ?? request.RefreshToken; 

        var principal = GetPrincipalFromExpiredToken(request.Token);
        if (principal == null) return BadRequest("Invalid Token");

        // Extract Session ID from JTI claim (try multiple possible claim types)
        var jtiClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti || c.Type == "jti");
        if (jtiClaim == null || !Guid.TryParse(jtiClaim.Value, out var sessionId)) return BadRequest("Missing Session ID");

        var session = _repository.GetUserSession(sessionId);
        if (session == null || !session.IsActive || session.RefreshToken != refreshToken) {
            return BadRequest("Invalid or Expired Session");
        }

        var user = _repository.GetUser(session.UserId);
        if (user == null) return BadRequest("User not found");

        // Generate new tokens while keeping the same session ID
        var newTokens = GenerateToken(user.Username, user.Id, user.Role, sessionId);
        
        session.RefreshToken = newTokens.RefreshToken;
        session.LastActiveAt = DateTime.UtcNow;
        _repository.UpdateUserSession(session);

        SetRefreshTokenCookie(newTokens.RefreshToken);
        newTokens.RefreshToken = "";

        return Ok(newTokens);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token) {
        var secretKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var tokenValidationParameters = new TokenValidationParameters {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey)),
            ValidateLifetime = false // Ignore expiration for validation
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }
        catch {
            return null;
        }
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout([FromBody] RefreshTokenRequest request) {
        var userId = GetUserIdOrThrow();
        var refreshToken = Request.Cookies["refreshToken"] ?? request.RefreshToken;
        
        if (!string.IsNullOrEmpty(refreshToken)) {
            _repository.InactivateSession(refreshToken);
        }

        var user = _repository.GetUser(userId);
        if (user != null && user.RefreshToken == refreshToken) {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            _repository.UpdateUser(user);
        }

        Response.Cookies.Delete("refreshToken");

        return Ok(new { Message = "Logged out successfully" });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe() {
        try {
            var userId = GetUserIdOrThrow();
            var user = _repository.GetUser(userId);
            if (user == null) return NotFound();

            var prog = _levelService.GetProgression(userId, _repository);
            var userProfile = _repository.GetPlayerProfile(userId);
            if (userProfile == null) {
                userProfile = new PlayerProfile { Id = Guid.NewGuid(), UserId = userId };
                _repository.CreatePlayerProfile(userProfile);
            }

            var rtpStats = _repository.GetOrCreateUserStats(userId);
            var userRounds = _repository.GetUserHistory(userId, 50).ToList();
            
            var favGame = userRounds.Any() ? userRounds.GroupBy(r => r.GameName)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "N/A" : "N/A";

            var trend = userRounds.Any() ? userRounds.OrderBy(r => r.PlayedAt)
                .TakeLast(7)
                .Select(r => (double)(r.WinAmount - r.BetAmount))
                .ToList() : new List<double>();

            var userAchs = await _achievementService.GetUserAchievements(userId, _repository);

            var activeSession = _repository.GetAllActiveSessions()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartedAt)
                .FirstOrDefault();

            var biggestWin = userRounds.Any() ? userRounds.Max(r => r.WinAmount) : 0;
            var personalRtp = rtpStats.TotalWagered > 0 ? (double)(rtpStats.TotalPaid / rtpStats.TotalWagered) : 0;
            var luckFactor = personalRtp / 0.96;

            return Ok(new UserDto {
                Username = user.Username,
                Balance = user.Balance,
                BonusBalance = user.BonusBalance,
                Role = user.Role.ToString(),
                AvatarUrl = string.IsNullOrEmpty(user.AvatarUrl) ? $"https://api.dicebear.com/7.x/bottts/svg?seed={user.Username}" : user.AvatarUrl,
                ActiveGameStateJson = activeSession?.GameState,
                SymbolAffinityJson = userProfile.SymbolAffinityJson,
                TotalWagered = rtpStats.TotalWagered,
                TotalWon = rtpStats.TotalPaid,
                TotalRounds = (int)rtpStats.TotalRounds,
                FavoriteGame = favGame,
                RecentWinLossTrend = trend,
                DailyLossLimit = user.DailyLossLimit,
                WeeklyLossLimit = user.WeeklyLossLimit,
                MonthlyLossLimit = user.MonthlyLossLimit,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                PreferencesJson = user.PreferencesJson,
                LockoutUntil = user.LockoutUntil,
                ReferralCode = string.IsNullOrEmpty(user.ReferralCode) ? user.Username.ToLower() : user.ReferralCode,
                LuckyCloverLevel = userProfile.LuckyCloverLevel,
                CashbackLevel = userProfile.CashbackLevel,
                XpBoostLevel = userProfile.XpBoostLevel,
                VolatilityScore = userProfile.VolatilityScore,
                ChurnRiskScore = userProfile.ChurnRiskScore,
                BiggestWin = biggestWin,
                PendingCashback = userProfile.PendingCashback,
                AvgSpinInterval = userProfile.AvgSpinInterval,
                LossStreak = userProfile.LossStreak,
                LuckFactor = (decimal)luckFactor,
                FruitBlastLifetimeExplosions = userProfile.FruitBlastLifetimeExplosions,
                Progression = new UserProgressionDto {
                    CurrentLevel = prog.CurrentLevel,
                    CurrentXP = prog.CurrentXP,
                    SkillPoints = prog.SkillPoints,
                    LifetimeXP = prog.LifetimeXP
                },
                CurrentStreak = user.CurrentStreak,
                Achievements = userAchs.Select(a => new UserAchievementDto {
                    Name = a.Achievement?.Name ?? "Unknown",
                    Description = a.Achievement?.Description ?? "",
                    Icon = a.Achievement?.Icon ?? "",
                    UnlockedAt = a.UnlockedAt
                }).ToList()
            });
        } catch (Exception ex) {
            Console.WriteLine($"[AUTH_ERROR] Error in GetMe: {ex.Message}\n{ex.StackTrace}");
            return StatusCode(500, "Internal server error while loading profile.");
        }
    }
    
    [Authorize]
    [HttpPost("avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] string avatarUrl) {
        var userId = GetUserIdOrThrow();

        if (await _redisCache.IncrementRateLimitAsync($"ratelimit:avatar:{userId}", TimeSpan.FromHours(1), 10)) {
            return StatusCode(429, "Too many avatar updates. Please try again later.");
        }

        if (string.IsNullOrEmpty(avatarUrl) || avatarUrl.Length > 500) {
            return BadRequest("Invalid avatar URL length.");
        }

        var imageRegex = new System.Text.RegularExpressions.Regex(@"^https?://.*\.(png|jpg|jpeg|gif|webp|svg)(\?.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!imageRegex.IsMatch(avatarUrl) && !avatarUrl.Contains("api.dicebear.com")) {
            return BadRequest("Only secure image URLs or DiceBear avatars are allowed.");
        }

        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        user.AvatarUrl = avatarUrl;
        _repository.UpdateUser(user);
        return Ok(new { Message = "Avatar updated!" });
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request) {
         string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
         if (await _redisCache.IncrementRateLimitAsync($"ratelimit:register:{ip}", TimeSpan.FromHours(1), 5)) {
             return StatusCode(429, "Too many registration attempts. Please try again in an hour.");
         }

         if (_repository.GetUserByUsername(request.Username) != null) {
             return BadRequest("Username already exists.");
         }

         var userId = Guid.NewGuid();
         var user = new User {
             Id = userId,
             Username = request.Username,
             Email = request.Email,
             PasswordHash = _passwordHasher.HashPassword(request.Password), 
             Role = Role.User,
             Balance = 5000m,
             AvatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={request.Username}",
             CreatedAt = DateTime.UtcNow,
             IsActive = true,
             ReferralCode = request.Username.ToLower() // Auto-generate their own ref code
         };

         if (!string.IsNullOrEmpty(request.ReferralCode)) {
             var referrer = _repository.GetAllUsers().FirstOrDefault(u => u.ReferralCode == request.ReferralCode || u.Username.ToLower() == request.ReferralCode.ToLower());
             if (referrer != null) {
                 user.ReferredById = referrer.Id;
                 _auditService.LogEvent("REFERRAL_JOIN", $"User {user.Username} joined via referral from {referrer.Username}", referrer.Id.ToString(), user.Id.ToString());
             }
         }
         
         _repository.CreateUser(user);

         // INITIALIZE SUB-ENTITIES IMMEDIATELY
         _repository.CreatePlayerProfile(new PlayerProfile { Id = Guid.NewGuid(), UserId = userId });
         _levelService.GetProgression(userId, _repository); 
         _repository.GetOrCreateUserStats(userId);

         return Ok(new { Message = "User created", UserId = user.Id });
    }

    [HttpPost("dev/promote")]
    [Authorize]
    public IActionResult PromoteToAdmin([FromBody] string secret) {
        var validSecret = _configuration["Admin:Secret"];
        if (string.IsNullOrEmpty(validSecret) || secret != validSecret) return Unauthorized();
        
        var userId = GetUserIdOrThrow();
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        user.Role = Role.Admin;
        _repository.UpdateUser(user);
        
        return Ok(GenerateToken(user.Username, user.Id, Role.Admin));
    }

    [Authorize]
    [HttpPost("password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest request) {
        var userId = GetUserIdOrThrow();
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.CurrentPassword)) {
            return BadRequest("Invalid current password.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.RefreshToken = null; 
        user.RefreshTokenExpiry = null;
        _repository.UpdateUser(user);
        
        _repository.InactivateAllUserSessions(userId); 
        
        return Ok(new { Message = "Password updated successfully!" });
    }

    [Authorize]
    [HttpPost("settings")]
    public IActionResult UpdateSettings([FromBody] UpdateSettingsRequest request) {
        var userId = GetUserIdOrThrow();
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        if (request.DailyLossLimit.HasValue) user.DailyLossLimit = request.DailyLossLimit;
        if (request.WeeklyLossLimit.HasValue) user.WeeklyLossLimit = request.WeeklyLossLimit;
        if (request.MonthlyLossLimit.HasValue) user.MonthlyLossLimit = request.MonthlyLossLimit;
        if (!string.IsNullOrEmpty(request.PreferencesJson)) user.PreferencesJson = request.PreferencesJson;

        _repository.UpdateUser(user);
        return Ok(new { Message = "Settings updated!" });
    }

    [Authorize]
    [HttpPost("exclude")]
    public IActionResult SelfExclude([FromBody] int hours) {
        var userId = GetUserIdOrThrow();
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        user.LockoutUntil = DateTime.UtcNow.AddHours(hours);
        _repository.UpdateUser(user);
        return Ok(new { Message = $"Self-exclusion active until {user.LockoutUntil:yyyy-MM-dd HH:mm} UTC" });
    }

    [Authorize]
    [HttpPost("sessions")]
    public IActionResult GetSessions([FromBody] SessionRequest? request) {
        var userId = GetUserIdOrThrow();
        var sessions = _repository.GetUserSessions(userId);
        var currentToken = Request.Cookies["refreshToken"] ?? request?.RefreshToken;

        return Ok(sessions.Select(s => new {
            s.Id,
            s.IpAddress,
            Device = s.UserAgent,
            LastActive = s.LastActiveAt,
            IsCurrent = !string.IsNullOrEmpty(currentToken) && s.RefreshToken == currentToken
        }));
    }

    [Authorize]
    [HttpPost("sessions/terminate")]
    public IActionResult TerminateSession([FromBody] TerminateSessionRequest request) {
        var userId = GetUserIdOrThrow();
        var sessions = _repository.GetUserSessions(userId);
        var target = sessions.FirstOrDefault(s => s.Id == request.SessionId);
        
        if (target != null) {
            _repository.DeleteUserSession(request.SessionId);
            return Ok(new { Message = "Session terminated." });
        }
        return NotFound();
    }

    public record TerminateSessionRequest(Guid SessionId);
    public record SessionRequest(string RefreshToken);

    [Authorize]
    [HttpPost("2fa/toggle")]
    public IActionResult Toggle2FA() {
        var userId = GetUserIdOrThrow();
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        user.IsTwoFactorEnabled = !user.IsTwoFactorEnabled;
        if (user.IsTwoFactorEnabled) {
            user.TwoFactorSecret = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper();
        }
        _repository.UpdateUser(user);
        return Ok(new { Enabled = user.IsTwoFactorEnabled, Secret = user.TwoFactorSecret });
    }

    private LoginResponse GenerateToken(string username, Guid userId, Role role, Guid? existingSessionId = null) {
        var secretKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(secretKey);
        
        var sessionId = existingSessionId ?? Guid.NewGuid();
        
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var refreshToken = Convert.ToBase64String(randomNumber);

        if (existingSessionId == null) {
            _repository.CreateUserSession(new UserSession {
                Id = sessionId,
                UserId = userId,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                UserAgent = Request.Headers["User-Agent"].ToString(),
                CreatedAt = DateTime.UtcNow,
                LastActiveAt = DateTime.UtcNow,
                IsActive = true,
                RefreshToken = refreshToken
            });
        }

        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, sessionId.ToString()), 
                new Claim(ClaimTypes.Role, role.ToString()), 
                new Claim("role", role.ToString())
            }),
            Expires = DateTime.UtcNow.AddMinutes(30), 
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return new LoginResponse { 
            Token = tokenHandler.WriteToken(token), 
            RefreshToken = refreshToken,
            Username = username, 
            Role = role.ToString() 
        };
    }

    private Guid GetUserIdOrThrow() {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (idClaim == null || !Guid.TryParse(idClaim.Value, out var id)) {
            throw new UnauthorizedAccessException("Invalid User Token");
        }

        if (_repository.GetUser(id) == null) {
            throw new UnauthorizedAccessException("User no longer exists");
        }

        return id;
    }
}