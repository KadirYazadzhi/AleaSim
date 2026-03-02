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

    public AuthController(IConfiguration configuration, IGameRepository repository, IPasswordHasher passwordHasher, ILevelService levelService, IAchievementService achievementService) {
        _configuration = configuration;
        _repository = repository;
        _passwordHasher = passwordHasher;
        _levelService = levelService;
        _achievementService = achievementService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request) {
        var user = _repository.GetUserByUsername(request.Username);
        
        if (user != null && _passwordHasher.VerifyPassword(user.PasswordHash, request.Password)) {
            var response = GenerateToken(user.Username, user.Id, user.Role);
            
            // Save Refresh Token to DB
            user.RefreshToken = response.RefreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            _repository.UpdateUser(user);

            return Ok(response);
        }
        
        return Unauthorized("Invalid credentials.");
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public IActionResult Refresh([FromBody] RefreshTokenRequest request) {
        var principal = GetPrincipalFromExpiredToken(request.Token);
        if (principal == null) return BadRequest("Invalid Token");

        var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return BadRequest("Invalid Token Claims");

        var user = _repository.GetUser(userId);
        if (user == null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiry < DateTime.UtcNow) {
            return BadRequest("Invalid or Expired Refresh Token");
        }

        var newTokens = GenerateToken(user.Username, user.Id, user.Role);
        
        // Rotate Refresh Token
        user.RefreshToken = newTokens.RefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        _repository.UpdateUser(user);

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
    [HttpGet("me")]
    public async Task<IActionResult> GetMe() {
        var userId = GetUserIdOrThrow();
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        // Get RPG Progress
        var prog = _levelService.GetProgression(userId, _repository);
        var userProfile = _repository.GetPlayerProfile(userId);

        // Get Real Stats
        var rtpStats = _repository.GetOrCreateUserStats(userId);
        var userRounds = _repository.GetUserHistory(userId, 50);
        
        var favGame = userRounds.GroupBy(r => r.GameName)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? "N/A";

        var trend = userRounds.OrderBy(r => r.PlayedAt)
            .TakeLast(7)
            .Select(r => (double)(r.WinAmount - r.BetAmount))
            .ToList();

        // Get Achievements
        var userAchs = await _achievementService.GetUserAchievements(userId, _repository);

        // Get Active Session for State Recovery
        var activeSession = _repository.GetAllActiveSessions()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        return Ok(new {
            user.Username,
            user.Balance,
            user.BonusBalance,
            AvatarUrl = string.IsNullOrEmpty(user.AvatarUrl) ? $"https://api.dicebear.com/7.x/bottts/svg?seed={user.Username}" : user.AvatarUrl,
            ActiveGameStateJson = activeSession?.GameState,
            Role = user.Role.ToString(),

            TotalWagered = rtpStats.TotalWagered,
            TotalWon = rtpStats.TotalPaid,
            TotalRounds = rtpStats.TotalRounds,
            FavoriteGame = favGame,
            RecentWinLossTrend = trend,

            user.DailyLossLimit,
            user.WeeklyLossLimit,
            user.IsTwoFactorEnabled,
            user.PreferencesJson,
            user.LockoutUntil,

            LuckyCloverLevel = userProfile?.LuckyCloverLevel ?? 0,
            CashbackLevel = userProfile?.CashbackLevel ?? 0,
            XpBoostLevel = userProfile?.XpBoostLevel ?? 0,
            Progression = new UserProgressionDto {
                CurrentLevel = prog.CurrentLevel,
                CurrentXP = prog.CurrentXP,
                SkillPoints = prog.SkillPoints,
                LifetimeXP = prog.LifetimeXP
            },
            CurrentStreak = user.CurrentStreak,
            Achievements = userAchs.Select(a => new UserAchievementDto {
                Name = a.Achievement.Name,
                Description = a.Achievement.Description,
                Icon = a.Achievement.Icon,
                UnlockedAt = a.UnlockedAt
            })
        });
    }
    
    [Authorize]
    [HttpPost("avatar")]
    public IActionResult UpdateAvatar([FromBody] string avatarUrl) {
        var userId = GetUserIdOrThrow();
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        user.AvatarUrl = avatarUrl;
        _repository.UpdateUser(user);
        return Ok(new { Message = "Avatar updated!" });
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request) {
         if (_repository.GetUserByUsername(request.Username) != null) {
             return BadRequest("Username already exists.");
         }

         var user = new User {
             Id = Guid.NewGuid(),
             Username = request.Username,
             Email = request.Email,
             PasswordHash = _passwordHasher.HashPassword(request.Password), 
             Role = Role.User,
             Balance = 1000m,
             AvatarUrl = $"https://api.dicebear.com/7.x/bottts/svg?seed={request.Username}",
             CreatedAt = DateTime.UtcNow,
             IsActive = true
         };
         
         _repository.CreateUser(user);
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
        
        // Return new token
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
        _repository.UpdateUser(user);
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
    [HttpPost("2fa/toggle")]
    public IActionResult Toggle2FA() {
        var userId = GetUserIdOrThrow();
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        user.IsTwoFactorEnabled = !user.IsTwoFactorEnabled;
        if (user.IsTwoFactorEnabled) {
            user.TwoFactorSecret = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper(); // Mock secret
        }
        _repository.UpdateUser(user);
        return Ok(new { Enabled = user.IsTwoFactorEnabled, Secret = user.TwoFactorSecret });
    }

    private LoginResponse GenerateToken(string username, Guid userId, Role role) {
        var secretKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(secretKey);
        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString()), new Claim("role", role.ToString()), new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", role.ToString())
            }),
            Expires = DateTime.UtcNow.AddHours(12), // Extended Access Token lifetime to 12 hours (was 4h)
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        // Generate Cryptographically Secure Refresh Token
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        var refreshToken = Convert.ToBase64String(randomNumber);

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
        return id;
    }
}