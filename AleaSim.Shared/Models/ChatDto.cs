using System;

namespace AleaSim.Shared.Models;

public class ChatMessageDto {
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderUsername { get; set; } = string.Empty;
    public string SenderAvatarUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsRead { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsEdited { get; set; }
}
