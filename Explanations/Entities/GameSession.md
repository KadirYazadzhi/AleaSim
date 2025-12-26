# GameSession - State & Seeding Entity

The `GameSession` class represents a continuous interaction period between a specific user and a specific game.

## 🎯 Purpose
1.  **State Tracking**: Required for multi-stage games (like Blackjack) where the server needs to remember the deck state between "Deal" and "Hit".
2.  **RNG Consistency**: Stores the `Seed` that governs the random numbers for the entire duration of play.

## 🏗️ Property Breakdown

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `Guid` | Unique session ID. |
| **`UserId`** | `Guid` | The player owner of the session. |
| **`GameId`** | `Guid` | The game being played. |
| **`Seed`** | `int` | **Critical for Fairness**. This integer initializes the Random Number Generator. <br>• In `DeterministicRngService`, this Seed + Round Number = Predictable (Audit-safe) Randomness. |
| **`StartedAt`** | `DateTime` | When the session opened. |
| **`EndedAt`** | `DateTime?` | When the session closed. Nullable because an active session hasn't ended yet. |
| **`IsActive`** | `bool` | Performance optimization. Allows the system to quickly query only open sessions. |

## 🔄 Lifecycle
1.  **Start**: User opens a game -> New Session created -> Seed generated.
2.  **Play**: Multiple `GameRound`s are created linked to this Session ID.
3.  **End**: User leaves or times out -> `EndedAt` is set, `IsActive` becomes false.