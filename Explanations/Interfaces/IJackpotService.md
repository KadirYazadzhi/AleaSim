# IJackpotService - Jackpot Manager Contract

The `IJackpotService` defines how the system handles progressive prize pools.

## 🎯 Purpose
To decouple the complex logic of "Who contributes what?" and "Did they win the big prize?" from the core game engines.

## 🛠️ Method Contracts

### `void Contribute(Guid gameId, decimal betAmount)`
- **Goal**: Accumulation.
- **Logic**: Called on *every* bet. Takes a percentage (defined in `Jackpot` entity) and adds it to the pot.
- **Concurrency**: Must be thread-safe (atomic increments) as thousands of users bet simultaneously.

### `bool CheckJackpotTrigger(Guid gameId, int seed, int sequence, out decimal winAmount)`
- **Goal**: Evaluation.
- **Logic**:
    1. Performs a separate RNG roll (distinct from the game outcome).
    2. Usually has very low odds (e.g., 1 in 1,000,000).
    3. If triggered, returns `true` and the current pot value (`winAmount`).
    4. **Reset**: The implementation must verify resetting the pot to its base value immediately to prevent double payouts.

### `Jackpot GetGlobalJackpot()` / `Jackpot GetLocalJackpot(Guid gameId)`
- **Goal**: State retrieval. Used to display "Current Jackpot: $1,234,567" on the UI.
