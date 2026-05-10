using AleaSim.Domain.Interfaces;
using AleaSim.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AleaSim.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase {
    private readonly IGameRepository _repo;

    public ChatController(IGameRepository repo) {
        _repo = repo;
    }

    [HttpGet("global")]
    public IActionResult GetGlobalHistory([FromQuery] int count = 50) {
        var history = _repo.GetGlobalChatMessages(count);
        return Ok(history.Select(m => new ChatMessageDto {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderUsername = m.SenderUsername,
            SenderAvatarUrl = m.SenderAvatarUrl,
            Message = m.Message,
            Timestamp = m.Timestamp,
            IsPrivate = false,
            IsRead = m.IsRead,
            IsDeleted = m.IsDeleted,
            IsEdited = m.IsEdited
        }));
    }

    [HttpGet("private/{otherUserId}")]
    public IActionResult GetPrivateHistory(Guid otherUserId, [FromQuery] int count = 50) {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) {
            return Unauthorized();
        }

        // Authorization check: One of them MUST be an admin
        var user = _repo.GetUser(userId);
        var otherUser = _repo.GetUser(otherUserId);
        
        if (user == null || otherUser == null) return NotFound();

        bool isAdmin = user.Role == AleaSim.Domain.Enums.Role.Admin || otherUser.Role == AleaSim.Domain.Enums.Role.Admin;
        if (!isAdmin) return Forbid("At least one party must be an admin for private chat.");

        var history = _repo.GetPrivateChatHistory(userId, otherUserId, count);
        return Ok(history.Select(m => new ChatMessageDto {
            Id = m.Id,
            SenderId = m.SenderId,
            SenderUsername = m.SenderUsername,
            SenderAvatarUrl = m.SenderAvatarUrl,
            Message = m.Message,
            Timestamp = m.Timestamp,
            IsPrivate = true,
            IsRead = m.IsRead,
            IsDeleted = m.IsDeleted,
            IsEdited = m.IsEdited
        }));
    }

    [HttpPost("private/{otherUserId}/read")]
    public IActionResult MarkAsRead(Guid otherUserId) {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) return Unauthorized();

        // Mark messages SENT BY otherUser TO userId as read
        _repo.MarkPrivateMessagesAsRead(otherUserId, userId);
        return Ok();
    }

    [HttpGet("conversations")]
    public IActionResult GetActiveConversations() {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId)) {
            return Unauthorized();
        }

        var user = _repo.GetUser(userId);
        if (user == null) return NotFound();

        // Fetch users who have private messages with this user
        var interlocutors = _repo.GetRecentPrivateInterlocutors(userId);
        
        return Ok(interlocutors.Select(i => new { Id = i.Id, Username = i.Username, AvatarUrl = i.AvatarUrl }));
    }
}
