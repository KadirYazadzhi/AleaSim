using System;

namespace AleaSim.Domain.Entities;

public class SupportMessage {
    public Guid Id { get; set; }
    public Guid? UserId { get; set; } // Null if guest
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
}
