using System.ComponentModel.DataAnnotations;

namespace AleaSim.Shared.Models;

public class StartSessionRequest {
    public Guid GameId { get; set; }
    public string? ClientSeed { get; set; }
}

public class StartSessionResponse {
    public Guid SessionId { get; set; }
    public Guid GameId { get; set; }
    public DateTime StartedAt { get; set; }
    public string ClientSeed { get; set; } = string.Empty;
    public string ServerSeedHash { get; set; } = string.Empty;
    public string? PreviousServerSeed { get; set; } // Revealed after rotation
    public object? GameState { get; set; } // Added for Resume Session
    
    public StartSessionResponse() {}
    public StartSessionResponse(Guid sessionId, Guid gameId, DateTime startedAt, string clientSeed, string serverSeedHash, string? previousServerSeed = null) {
        SessionId = sessionId;
        GameId = gameId;
        StartedAt = startedAt;
        ClientSeed = clientSeed;
        ServerSeedHash = serverSeedHash;
        PreviousServerSeed = previousServerSeed;
    }
}

public class PlaceBetRequest {
    [Range(0, 1000000, ErrorMessage = "Bet amount must be between 0 and 1,000,000.")]
    public decimal Amount { get; set; }
    public object? BetData { get; set; }
}
public class PlaceBetResponse {
    public Guid RoundId { get; set; }
    public decimal TotalWin { get; set; }
    public string RandomResultJson { get; set; } = string.Empty;
    public bool IsJackpotWin { get; set; }
    
    // New UX Properties
    public float FlowStateIntensity { get; set; } // 0.0 to 1.0 (Based on spin speed)
    public bool IsNearMiss { get; set; }

    public PlaceBetResponse() {}
    public PlaceBetResponse(Guid roundId, decimal totalWin, string randomResultJson, bool isJackpotWin) {
        RoundId = roundId;
        TotalWin = totalWin;
        RandomResultJson = randomResultJson;
        IsJackpotWin = isJackpotWin;
    }
}

public class GameActionRequest {
    public string Action { get; set; } = string.Empty;
    public object? ActionData { get; set; } 
}

public class GameActionResponse {
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string UpdatedStateJson { get; set; } = string.Empty;

    public GameActionResponse() {}
    public GameActionResponse(bool success, string message, string updatedStateJson) {
        Success = success;
        Message = message;
        UpdatedStateJson = updatedStateJson;
    }
}

public class RouletteBetDto {
    public string Type { get; set; } = string.Empty; // "number", "color", "evenodd"
    public string Value { get; set; } = string.Empty; // "17", "red", "even"

    [Range(0, 1000000, ErrorMessage = "Bet amount must be between 0 and 1,000,000.")]
    public decimal Amount { get; set; }
}
public class GameRoundDto {
    public Guid Id { get; set; }
    public string GameName { get; set; } = string.Empty;
    public decimal BetAmount { get; set; }
    public decimal WinAmount { get; set; }
    public string ResultSummary { get; set; } = string.Empty;
    public string FullResultJson { get; set; } = string.Empty; // Added for Replay
    public DateTime PlayedAt { get; set; }
    public string? ServerSeed { get; set; }
    public string? ServerSeedHash { get; set; }
    public string? ClientSeed { get; set; }
    public int Nonce { get; set; }
    public bool IsWin => WinAmount > 0;
}

public class GameHistoryDto : GameRoundDto {
}

public class LeaderboardEntry {
    public string Username { get; set; } = "";
    public decimal Score { get; set; }
    public int Rank { get; set; }
    public string AvatarUrl { get; set; } = "";
}

public class DailyBonusResponse {
    public decimal PrizeAmount { get; set; }
    public string PrizeType { get; set; } = "Cash"; // Cash, Spins, XP
    public bool IsJackpot { get; set; }
    public int SegmentIndex { get; set; }
}

public class DiceBetDto {
    public string Mode { get; set; } = "Slider"; // "Slider" or "Multi"
    public decimal TargetValue { get; set; } // Slider: 0-100
    public string Condition { get; set; } = "Over"; // Slider: "Over", "Under"
    public List<int>? MultiDiceSelected { get; set; } // Multi: Selected numbers 1-6
}

public class DiceResultDto {
    public decimal ResultValue { get; set; } // Slider: 0.00-100.00
    public List<int>? MultiDiceResults { get; set; } // Multi: 10 dice results
    public decimal PayoutMultiplier { get; set; }
    public bool IsWin { get; set; }
}

public class PlatformStatsDto {
    public decimal WeeklyJackpot { get; set; }
    public decimal TournamentPrizePool { get; set; }
    public DateTime TournamentEndsAt { get; set; }
    public int ActivePlayers { get; set; }
    public int TotalRegisteredPlayers { get; set; }
    public double AverageRtp { get; set; }
    public decimal TotalRewardsPaid { get; set; }
    public int CurrentSeason { get; set; }
    public List<string> TopTournamentAvatars { get; set; } = new();
}