using AleaSim.Domain.Entities;
using AleaSim.Domain.Enums;

namespace AleaSim.Domain.Interfaces;

public interface IGame {
    Task<GameSession> StartSession(Guid userId, Guid gameId, int? seed = null, string? clientSeed = null);
    Task PlaceBet(Guid userId, Guid sessionId, decimal amount, string? betData);
    Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard);
    Task<Outcome> GetOutcome(Guid roundId);
    Task ProcessAction(Guid userId, Guid sessionId, string action, string actionData);
    Task<object?> GetCurrentState(Guid sessionId);
}