using AleaSim.Domain.Entities;
using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AleaSim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupportController : ControllerBase {
    private readonly IGameRepository _repo;

    public SupportController(IGameRepository repo) {
        _repo = repo;
    }

    [HttpPost("send")]
    [AllowAnonymous]
    public IActionResult SendMessage([FromBody] SupportMessageRequest request) {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        Guid? userId = null;
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var id)) {
            userId = id;
        }

        var message = new SupportMessage {
            Id = Guid.NewGuid(),
            UserId = userId,
            SenderName = request.Name,
            SenderEmail = request.Email,
            Subject = request.Subject,
            Message = request.Message,
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
