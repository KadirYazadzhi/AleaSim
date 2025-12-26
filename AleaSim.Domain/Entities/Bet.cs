namespace AleaSim.Domain.Entities;

public class Bet {
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid UserId { get; set; }
    public Guid GameRoundId { get; set; } // Can be nullable if bet is placed before round exists
    public decimal Amount { get; set; }
    public string BetData { get; set; } = string.Empty; // JSON or specific data for the game type
    public DateTime CreatedAt { get; set; }
}
