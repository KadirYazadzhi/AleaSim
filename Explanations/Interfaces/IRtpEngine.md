# IRtpEngine - Math Assurance Contract

The `IRtpEngine` defines the regulatory brakes for the system.

## 🎯 Purpose
Pure randomness can sometimes lead to statistical anomalies (e.g., a player winning the jackpot 3 times in a row). While theoretically possible, it bankrupts casinos. The RTP Engine monitors this and can flag or block outcomes that are statistically impossible or indicate a system flaw.

## 🛠️ Method Contracts

### `bool IsOutcomeAllowed(Guid gameId, Guid userId, decimal potentialWinAmount, decimal betAmount)`
- **Goal**: Gatekeeping.
- **Logic**: Calculates the *hypothetical* new RTP if this win were to be paid.
- **Return**:
    - `true`: The win is within statistical tolerance.
    - `false`: The win deviates too far from the `TargetRTP`. The Game Engine should reroll or force a loss.

### `void RecordBet(...)` / `void RecordWin(...)`
- **Goal**: Data ingestion. Updates the `RTPStatistics` counters.

### `RTPStatistics GetGameStats(Guid gameId)`
- **Goal**: Monitoring. Returns the aggregate performance of a game.
