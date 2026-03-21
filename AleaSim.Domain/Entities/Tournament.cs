namespace AleaSim.Domain.Entities;

public class Tournament {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal PrizePool { get; set; }
    public bool IsActive { get; set; }
    public string GameTypesJson { get; set; } = "[]"; // List of games included in tournament
}
