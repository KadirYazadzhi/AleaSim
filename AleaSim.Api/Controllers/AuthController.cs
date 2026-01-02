using AleaSim.Shared.Models; // Changed
using AleaSim.Domain.Enums;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services; // Added
using Microsoft.AspNetCore.Authorization; // Added
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AleaSim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase {
    private readonly IConfiguration _configuration;
    private readonly IGameRepository _repository;
    private readonly IPasswordHasher _passwordHasher;

    public AuthController(IConfiguration configuration, IGameRepository repository, IPasswordHasher passwordHasher) {
        _configuration = configuration;
        _repository = repository;
        _passwordHasher = passwordHasher;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request) {
        var user = _repository.GetUserByUsername(request.Username);
        
        if (user != null && _passwordHasher.VerifyPassword(request.Password, user.PasswordHash)) { // Fixed order
            return Ok(GenerateToken(user.Username, user.Id, user.Role));
        }
        
        return Unauthorized("Invalid credentials.");
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe() {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        // Get RPG Progress
        var levelService = HttpContext.RequestServices.GetRequiredService<ILevelService>();
        var prog = levelService.GetProgression(userId, _repository);
        var userProfile = _repository.GetPlayerProfile(userId); // Added to get skill levels

        // Get Achievements
        var achService = HttpContext.RequestServices.GetRequiredService<IAchievementService>();
        var userAchs = await achService.GetUserAchievements(userId, _repository);

                return Ok(new {

                    user.Username,

                    user.Balance,

                    user.BonusBalance,

                    user.AvatarUrl, // Added

                    Role = user.Role.ToString(),

                    LuckyCloverLevel = userProfile?.LuckyCloverLevel ?? 0,

         // Using userProfile from repo/service
            CashbackLevel = userProfile?.CashbackLevel ?? 0,
            XpBoostLevel = userProfile?.XpBoostLevel ?? 0,
            Progression = new UserProgressionDto {
                CurrentLevel = prog.CurrentLevel,
                CurrentXP = prog.CurrentXP,
                SkillPoints = prog.SkillPoints,
                LifetimeXP = prog.LifetimeXP
            },
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
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
        var user = _repository.GetUser(userId);
        if (user == null) return NotFound();

        user.AvatarUrl = avatarUrl;
        _repository.UpdateUser(user);
        return Ok(new { Message = "Avatar updated!" });
    }

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
             CreatedAt = DateTime.UtcNow,
             IsActive = true
         };
         
         _repository.CreateUser(user);
         return Ok(new { Message = "User created", UserId = user.Id });
    }

    private LoginResponse GenerateToken(string username, Guid userId, Role role) {
        var secretKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(secretKey);
        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return new LoginResponse { 
            Token = tokenHandler.WriteToken(token), 
            Username = username, 
            Role = role.ToString() 
        };
    }
}

