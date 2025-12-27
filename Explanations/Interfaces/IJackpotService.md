# IJackpotService Interface Explanation

The `IJackpotService` interface manages the complex logic of progressive pools.

## 🛠️ Method Contracts

### `Contribute`
- **Logic**: Takes a slice of the bet (e.g., 1%) and adds it to the pool.
- **Persistence**: Updates the database record via `IGameRepository`.

### `CheckJackpotTrigger`
- **Logic**: Performs a separate RNG roll to see if the user wins the pot.
- **Safety**: Should be thread-safe to prevent multiple users winning the same pot simultaneously.

### `GetGlobalJackpot` / `GetLocalJackpot`
- **Purpose**: Retrieval for display in the game lobby or UI.