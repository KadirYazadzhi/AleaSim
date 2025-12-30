using AleaSim.Domain.Entities;
using AleaSim.Domain.Enums;

namespace AleaSim.Domain.Interfaces;

public interface IGame {
    Task<GameSession> StartSession(Guid userId, int? seed = null);
    Task PlaceBet(Guid sessionId, decimal amount, string betData);
    Task<GameRound> ResolveRound(Guid sessionId, SpinProfile profile = SpinProfile.Standard);
    Task<Outcome> GetOutcome(Guid roundId);
    
    // Interactive games (Blackjack/Poker)
    Task ProcessAction(Guid sessionId, string action, string actionData);
    Task<object?> GetCurrentState(Guid sessionId);
}
