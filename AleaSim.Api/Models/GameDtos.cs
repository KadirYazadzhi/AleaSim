namespace AleaSim.Api.Models;

public record StartSessionRequest(Guid GameId);
public record StartSessionResponse(Guid SessionId, Guid GameId, DateTime StartedAt);

public record PlaceBetRequest(decimal Amount, object BetData);
public record PlaceBetResponse(Guid RoundId, decimal TotalWin, object Result, bool IsJackpotWin);

public record GameActionRequest(string Action, string ActionData);
public record GameActionResponse(bool Success, string Message, object UpdatedState);
