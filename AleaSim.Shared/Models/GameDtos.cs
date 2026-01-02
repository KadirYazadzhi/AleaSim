namespace AleaSim.Shared.Models;

public class StartSessionRequest {
    public Guid GameId { get; set; }
}

public class StartSessionResponse {
    public Guid SessionId { get; set; }
    public Guid GameId { get; set; }
    public DateTime StartedAt { get; set; }
    
    public StartSessionResponse() {}
    public StartSessionResponse(Guid sessionId, Guid gameId, DateTime startedAt) {
        SessionId = sessionId;
        GameId = gameId;
        StartedAt = startedAt;
    }
}

public class PlaceBetRequest {
    public decimal Amount { get; set; }
    public string BetData { get; set; } = string.Empty;
}

public class PlaceBetResponse {
    public Guid RoundId { get; set; }
    public decimal TotalWin { get; set; }
    public string RandomResultJson { get; set; } = string.Empty;
    public bool IsJackpotWin { get; set; }

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
    public string ActionData { get; set; } = string.Empty;
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
