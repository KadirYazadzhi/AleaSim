namespace AleaSim.Domain.Entities;

public class Bet {
    public Guid Id { get; set; }
    public Guid GameRoundId { get; set; }
    public decimal Amount { get; set; }
    public string BetData { get; set; } = string.Empty; // JSON or specific data for the game type
    public DateTime CreatedAt { get; set; }
}
