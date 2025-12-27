# IRtpEngine Interface Explanation

The `IRtpEngine` interface defines the math guardrails for the casino. It ensures the system adheres to the configured Return to Player (RTP) percentages.

## 🛠️ Method Contracts

### `bool ProcessWin(...)`
- **Parameters**: `gameId`, `userId`, `winAmount`, `betAmount`, `IGameRepository`.
- **Logic**:
    1.  Calculates the projected RTP if this win is allowed.
    2.  If within tolerance, it records the stats and returns `true`.
    3.  If it violates the safety margin (e.g., RTP > 105%), it returns `false`, signaling the game engine to re-roll or force a loss.
- **Note**: Now accepts `IGameRepository` to fetch and update stats transactionally.

### `void RecordBet(...)`
- **Purpose**: Updates the "Money In" counter (`TotalWagered`).

### `RTPStatistics GetGameStats(...)`
- **Purpose**: Retrieval for monitoring dashboards.