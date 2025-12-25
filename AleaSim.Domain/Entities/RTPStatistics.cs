namespace AleaSim.Domain.Entities;

public class RTPStatistics {
    public Guid Id { get; set; }
    public Guid? GameId { get; set; } // Null if global
    public Guid? UserId { get; set; } // Null if aggregate for all users
    public decimal TotalWagered { get; set; }
    public decimal TotalPaid { get; set; }
    public double CurrentRTP => TotalWagered == 0 ? 0 : (double)(TotalPaid / TotalWagered);
    public long TotalRounds { get; set; }
    public DateTime LastCalculated { get; set; }
}
