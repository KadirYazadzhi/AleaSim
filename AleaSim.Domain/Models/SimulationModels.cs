namespace AleaSim.Domain.Models;

public class SimulationRequest {
    public string GameType { get; set; } = "Slot";
    public decimal BetAmount { get; set; } = 1.0m;
    public int Iterations { get; set; } = 100000;
    public Guid? UserId { get; set; } // Optional: simulate a specific player's profile
}

public class SimulationReport {
    public string GameType { get; set; } = string.Empty;
    public int TotalIterations { get; set; }
    public decimal TotalBet { get; set; }
    public decimal TotalWin { get; set; }
    public double ActualRTP { get; set; }
    public decimal MaxWin { get; set; }
    public int BonusGamesTriggered { get; set; }
    public int RespinsTriggered { get; set; }
    public Dictionary<string, int> DecisionDistribution { get; set; } = new();
    public double ExecutionTimeMs { get; set; }
}
