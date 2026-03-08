using System;

namespace AleaSim.Shared.Models;

public class SupportMessageDto {
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
