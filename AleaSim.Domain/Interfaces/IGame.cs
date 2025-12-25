using AleaSim.Domain.Entities;

namespace AleaSim.Domain.Interfaces;

public interface IGame {
    GameSession StartSession(Guid userId, int? seed = null);
    void PlaceBet(Guid sessionId, decimal amount, string betData);
    GameRound ResolveRound(Guid sessionId);
    Outcome GetOutcome(Guid roundId);
    void ProcessAction(Guid sessionId, string action, string actionData);
}
