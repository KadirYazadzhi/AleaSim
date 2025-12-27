# GameRound Entity Explanation

The `GameRound` class represents a single atomic unit of gameplay (e.g., one spin, one hand). It serves as the "Source of Truth" for game results.

## 📦 Properties

### Context
- **`Id`** (`Guid`): Unique round ID.
- **`GameSessionId`** (`Guid`): Links to the active session.
- **`RoundNumber`** (`int`): Sequential index (1, 2, 3...) within the session. Critical for deterministic RNG sequencing.

### Game Data
- **`InputData`** (`string`): Snapshot of the user's choices (e.g., bets placed, cards held).
- **`RandomResult`** (`string`): The raw output from the RNG (e.g., "Reel1: 5, Reel2: 3...").
    - **Verification**: Can be regenerated using `Session.Seed` + `RoundNumber`.

### Financials
- **`TotalBetAmount`** (`decimal`): Total money wagered in this round.
- **`TotalWinAmount`** (`decimal`): Total money paid out.
- **`ExecutedAt`** (`DateTime`): Timestamp of completion.
