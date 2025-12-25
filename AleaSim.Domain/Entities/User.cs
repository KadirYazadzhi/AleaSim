using AleaSim.Domain.Enums;

namespace AleaSim.Domain.Entities;

public class User {
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public Role Role { get; set; }
    public DateTime CreatedAt { get; set; }
}
