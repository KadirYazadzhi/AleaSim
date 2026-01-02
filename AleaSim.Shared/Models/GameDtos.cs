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
    
    public StartSessionResponse() {}
    public StartSessionResponse(Guid sessionId, Guid gameId, DateTime startedAt, string clientSeed, string serverSeedHash) {
        SessionId = sessionId;
        GameId = gameId;
        StartedAt = startedAt;
        ClientSeed = clientSeed;
        ServerSeedHash = serverSeedHash;
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

public class RouletteBetDto {
    public string Type { get; set; } = string.Empty; // "number", "color", "evenodd"
    public string Value { get; set; } = string.Empty; // "17", "red", "even"
    public decimal Amount { get; set; }
}

public class GameRoundDto {
    public Guid Id { get; set; }
    public string GameName { get; set; } = string.Empty;
    public decimal BetAmount { get; set; }
    public decimal WinAmount { get; set; }
    public string ResultSummary { get; set; } = string.Empty; // e.g. "Winning Number: 17" or "Grid Result"
    public DateTime PlayedAt { get; set; }
    public bool IsWin => WinAmount > 0;
}
