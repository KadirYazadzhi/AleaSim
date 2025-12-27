# GameController Explanation

`GameController.cs` is the primary entry point for gameplay. It is "Game Agnostic", meaning the same endpoint works for Slots, Roulette, and Blackjack.

## 🧩 Strategy Pattern Injection
It uses a `Func<string, IGame>` resolver to dynamically find the correct engine.
- **Request**: `POST api/game/slot/session` -> Resolves `SlotGameEngine`.
- **Request**: `POST api/game/blackjack/session` -> Resolves `BlackjackGameEngine`.

## 🛠️ Endpoints

### `POST api/game/{gameType}/session`
- **Action**: Starts a new game session.
- **Audit**: Logs `SESSION_START`.

### `POST api/game/{gameType}/bet/{sessionId}`
- **Action**: Places a wager.
- **Flow**:
    1.  Resolves Engine.
    2.  Calls `game.PlaceBet` (Transaction: Deduct Balance).
    3.  Calls `game.ResolveRound` (Transaction: Spin RNG, Calculate Win, Update Balance).
    4.  Logs `BET_PLACED` with win details.
- **Return**: The result of the round (e.g., Slot symbols).

### `POST api/game/{gameType}/action/{sessionId}`
- **Action**: Handles intermediate moves (Hit, Stand).
- **Flow**: Calls `game.ProcessAction` and then `game.GetCurrentState` to return the updated board.
