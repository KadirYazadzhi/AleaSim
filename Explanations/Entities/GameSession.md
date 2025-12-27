# GameSession Entity Explanation

The `GameSession` class tracks a continuous period of interaction between a user and a game. It is essential for stateful games and RNG seeding.

## 📦 Properties

### Links
- **`Id`** (`Guid`): Unique session ID.
- **`UserId`** (`Guid`): The player.
- **`GameId`** (`Guid`): The game being played.

### RNG & State
- **`Seed`** (`int`): The master seed for the Random Number Generator.
    - **Usage**: Combined with the `RoundNumber`, this allows the entire session to be deterministically replayed for verification.
- **`IsActive`** (`bool`): Indicates if the session is currently open.

### Lifecycle
- **`StartedAt`** (`DateTime`): Session start time.
- **`EndedAt`** (`DateTime?`): Nullable end time. Populated when the user leaves or times out.
