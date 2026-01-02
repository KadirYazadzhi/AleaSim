namespace AleaSim.Shared.Models;

public class ActiveSessionDto {
    public Guid SessionId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public decimal TotalWagered { get; set; }
    public decimal TotalWon { get; set; }
    public decimal NetResult => TotalWon - TotalWagered;
}
