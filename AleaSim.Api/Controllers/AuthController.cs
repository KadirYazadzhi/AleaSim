using AleaSim.Api.Models;
using AleaSim.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AleaSim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase {
    private const string SecretKey = "ThisIsASecretKeyForAleaSimSimulationProject2025!";

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request) {
        // Mock Authentication Logic
        if (request.Username == "admin" && request.Password == "admin") {
            return Ok(GenerateToken(request.Username, Role.Admin));
        }
        else if (request.Username == "user" && request.Password == "user") {
            return Ok(GenerateToken(request.Username, Role.User));
        }

        return Unauthorized();
    }

    private LoginResponse GenerateToken(string username, Role role) {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(SecretKey);
        var tokenDescriptor = new SecurityTokenDescriptor {
            Subject = new ClaimsIdentity(new[] {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role.ToString())
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return new LoginResponse(tokenHandler.WriteToken(token), username, role.ToString());
    }
}
