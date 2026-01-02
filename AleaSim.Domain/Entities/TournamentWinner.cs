namespace AleaSim.Domain.Entities;

public class TournamentWinner {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public int Rank { get; set; }
    public decimal PrizeAmount { get; set; }
    public decimal Score { get; set; } // The multiplier or points they won with
    public DateTime Month { get; set; } // The first day of the tournament month
}
