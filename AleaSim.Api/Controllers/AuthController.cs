using AleaSim.Api.Models;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services; // Added
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
        
        if (user != null && _passwordHasher.VerifyPassword(user.PasswordHash, request.Password)) {
            return Ok(GenerateToken(user.Username, user.Id, user.Role));
        }

        // Fallback check for admin (if Seed didn't run or DB empty, though Seed handles hashing now)
        // We will remove the plain text check to force usage of Seeded admin.
        
        return Unauthorized("Invalid credentials.");
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
             PasswordHash = _passwordHasher.HashPassword(request.Password), // Hashed!
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
        return new LoginResponse(tokenHandler.WriteToken(token), username, role.ToString());
    }
}

