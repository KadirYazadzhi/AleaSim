namespace AleaSim.Shared.Models;

public class AdminDashboardStats {
    public decimal Ggr { get; set; } 
    public decimal TotalBets { get; set; }
    public decimal TotalWins { get; set; }
    public decimal CurrentRtp { get; set; }
    public int ActivePlayerCount { get; set; }
    public bool IsEmergencyStopActive { get; set; }
    public List<string> TopWinners { get; set; } = new();
    public List<GameStatDto> GameStats { get; set; } = new();
    public List<PlayerRankDto> TopPlayers { get; set; } = new();
    public decimal NetProfit => TotalBets - TotalWins;
}

public class GameStatDto {
    public string GameName { get; set; } = string.Empty;
    public string GameType { get; set; } = string.Empty;
    public decimal TotalWagered { get; set; }
    public decimal TotalWon { get; set; }
    public decimal MaxWin { get; set; }
    public double Rtp => TotalWagered > 0 ? (double)(TotalWon / TotalWagered) * 100 : 0;
}

public class PlayerRankDto {
    public string Username { get; set; } = string.Empty;
    public decimal TotalWagered { get; set; }
    public decimal TotalWon { get; set; }
    public decimal Profit { get; set; }
}

public class RtpTrendPoint {
    public string Label { get; set; } = string.Empty;
    public double Rtp { get; set; }
}

public class ShadowCompareDto {
    public decimal RealTotalWin { get; set; }
    public decimal ShadowTotalWin { get; set; }
    public double RealRtp { get; set; }
    public double ShadowRtp { get; set; }
    public int SampleSize { get; set; }
}

public class SentinelAlertDto {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty; // "BotDetection", "HighPayout", "SuspiciousActivity"
    public string Severity { get; set; } = "Medium"; // "Low", "Medium", "High", "Critical"
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class SimulationRequest {
    public string GameType { get; set; } = "Slot";
    public string? GameMode { get; set; }
    public decimal BetAmount { get; set; } = 1.0m;
    public int Iterations { get; set; } = 100000;
    public Guid? UserId { get; set; }
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
    public List<SimulationDetail> DetailedResults { get; set; } = new();
}

public class SimulationDetail {
    public decimal BetAmount { get; set; }
    public decimal WinAmount { get; set; }
    public string DecisionType { get; set; } = "Random";
}
