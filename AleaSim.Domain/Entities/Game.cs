namespace AleaSim.Domain.Entities;

public class Game {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MinBet { get; set; }
    public decimal MaxBet { get; set; }
    public double TargetRTP { get; set; }
    public bool IsActive { get; set; }
}
