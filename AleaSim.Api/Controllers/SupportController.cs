using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Services;
using AleaSim.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AleaSim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupportController : ControllerBase {
    private readonly IGameRepository _repo;
    private readonly IRedisCacheService _redisCache;

    public SupportController(IGameRepository repo, IRedisCacheService redisCache) {
        _repo = repo;
        _redisCache = redisCache;
    }

    [HttpPost("send")]
    [AllowAnonymous]
    public async Task<IActionResult> SendMessage([FromBody] SupportMessageRequest request) {
        // Rate Limiting (10 per hour per IP)
        string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        if (await _redisCache.IncrementRateLimitAsync($"ratelimit:support:{ip}", TimeSpan.FromHours(1), 10)) {
            return StatusCode(429, "Too many support messages. Please try again later.");
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        Guid? userId = null;
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var id)) {
            userId = id;
        }

        // SECURITY: Sanitize user input to prevent XSS attacks
        var sanitizer = new Ganss.Xss.HtmlSanitizer();
        sanitizer.AllowedTags.Clear(); // No HTML tags allowed in support messages

        var message = new SupportMessage {
            Id = Guid.NewGuid(),
            UserId = userId,
            SenderName = sanitizer.Sanitize(request.Name),
            SenderEmail = sanitizer.Sanitize(request.Email),
            Subject = sanitizer.Sanitize(request.Subject),
            Message = sanitizer.Sanitize(request.Message),
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _repo.SaveSupportMessage(message);
        return Ok(new { Success = true, Message = "Message sent successfully!" });
    }

    [HttpGet("messages")]
    [Authorize(Roles = "Admin")]
    public IActionResult GetMessages([FromQuery] int count = 50) {
        var messages = _repo.GetSupportMessages(count);
        var result = messages.Select(m => new SupportMessageDto {
            Id = m.Id,
            UserId = m.UserId,
            SenderName = m.SenderName,
            SenderEmail = m.SenderEmail,
            Subject = m.Subject,
            Message = m.Message,
            CreatedAt = m.CreatedAt,
            IsRead = m.IsRead
        }).ToList();
        return Ok(result);
    }

    [HttpPost("messages/{id}/read")]
    [Authorize(Roles = "Admin")]
    public IActionResult MarkAsRead(Guid id) {
        _repo.MarkSupportMessageRead(id);
        return Ok();
    }
}

public class SupportMessageRequest {
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
