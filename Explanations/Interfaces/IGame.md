# IGame Interface Explanation

The `IGame` interface is the core contract for the Strategy Pattern implementation of different game types. It standardizes how the system interacts with any game logic, whether it's Slots, Blackjack, or Roulette.

## 🛠️ Method Contracts

### Session Lifecycle
- **`StartSession`**: Initializes a new session, setting up the RNG seed and returning the session object.

### Interaction
- **`PlaceBet`**: Validates parameters and records the player's wager.
- **`ProcessAction`**: Handles intermediate steps in stateful games (e.g., "Hit" or "Stand" in Blackjack).

### Execution
- **`ResolveRound`**: The "Spin" button. It calculates the random result, applies game rules, determines winnings, and returns the closed round.
- **`GetOutcome`**: Formats the final round data into a client-friendly `Outcome` object.

### State Inspection
- **`GetCurrentState`**: *New Method*.
    - **Purpose**: Returns the current in-memory state of an active round (e.g., showing the dealer's face-up card *before* the round is over).
    - **Return Type**: `object?` (Weakly typed to allow any game state structure, usually serialized to JSON by the controller).