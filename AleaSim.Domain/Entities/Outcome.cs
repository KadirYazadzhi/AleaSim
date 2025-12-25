namespace AleaSim.Domain.Entities;

public class Outcome {
    public Guid Id { get; set; }
    public Guid GameRoundId { get; set; }
    public string ResultJson { get; set; } = string.Empty;
    public decimal WinAmount { get; set; }
    public bool IsJackpotWin { get; set; }
}
