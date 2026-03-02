using System;

namespace AleaSim.Domain.Entities;

public class UserSession {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RefreshToken { get; set; }

    public virtual User User { get; set; } = null!;
}
