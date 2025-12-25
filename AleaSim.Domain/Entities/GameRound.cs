namespace AleaSim.Domain.Entities;

public class GameRound {
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public int RoundNumber { get; set; }
    public string InputData { get; set; } = string.Empty;
    public string RandomResult { get; set; } = string.Empty;
    public decimal TotalBetAmount { get; set; }
    public decimal TotalWinAmount { get; set; }
    public DateTime ExecutedAt { get; set; }
}
