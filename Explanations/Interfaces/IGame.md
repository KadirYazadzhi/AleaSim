# IGame - Game Engine Contract

The `IGame` interface is the backbone of the **Strategy Pattern** used in AleaSim. It allows the system to treat all games (Slots, Poker, Roulette) uniformly.

## 🎯 Purpose
The main API controller doesn't need to know *how* Blackjack works. It just calls `ResolveRound`. This interface creates a standard "Plug-and-Play" socket for any new game type.

## 🛠️ Method Contracts

### `GameSession StartSession(Guid userId, int? seed = null)`
- **Goal**: Handshake. Prepares the server for a new game interaction.
- **Details**: Generates the RNG seed. If a `seed` is provided (for testing/replay), it must use it.

### `void PlaceBet(Guid sessionId, decimal amount, string betData)`
- **Goal**: Commitment.
- **Validation**: Must check:
    1. Is session active?
    2. Does user have balance?
    3. Is `betData` valid for this game type? (e.g., "Is 'Red' a valid Roulette bet?")
- **Effect**: Deducts balance *immediately*.

### `GameRound ResolveRound(Guid sessionId)`
- **Goal**: Execution.
- **Logic**:
    1. Uses `IRngService` to generate the result.
    2. Calculates Winnings.
    3. Checks `IRtpEngine` to see if the win is allowed.
    4. Awards Balance.
    5. Returns the closed `GameRound` record.

### `Outcome GetOutcome(Guid roundId)`
- **Goal**: Reporting.
- **Usage**: Transforms the internal `GameRound` into the client-facing `Outcome` object.

### `void ProcessAction(Guid sessionId, string action, string actionData)`
- **Goal**: Interactivity.
- **Usage**: Only for multi-step games (e.g., Blackjack, Poker).
- **Example**: `action="Hit"`, `action="Fold"`. Single-step games (Slots) usually leave this empty.
