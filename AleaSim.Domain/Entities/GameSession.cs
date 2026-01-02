namespace AleaSim.Domain.Entities;

public class GameSession {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public int Seed { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
    
    // Serialized state for complex games (e.g., Slot Respins, Blackjack Hand)
    public string GameState { get; set; } = string.Empty;
}
