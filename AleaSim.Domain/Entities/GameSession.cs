namespace AleaSim.Domain.Entities;

public class GameSession {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public int Seed { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
    
    // --- Provably Fair ---
    public string ServerSeed { get; set; } = string.Empty; // Secret until end of session
    public string ClientSeed { get; set; } = string.Empty; // User provided
    public string ServerSeedHash { get; set; } = string.Empty; // Publicly visible
    
    // Serialized state for complex games (e.g., Slot Respins, Blackjack Hand)
    public string GameState { get; set; } = string.Empty;

    public decimal TotalWagered { get; set; }
    public decimal TotalWon { get; set; }
}
