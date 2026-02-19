namespace AleaSim.Shared.Models;

public class TournamentRankDto {
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public decimal MaxMultiplier { get; set; }
    public decimal TotalPaid { get; set; }
}

public class TournamentHistoryDto {
    public string MonthName { get; set; } = string.Empty;
    public string WinnerName { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public decimal Prize { get; set; }
    public decimal Multiplier { get; set; }
}
