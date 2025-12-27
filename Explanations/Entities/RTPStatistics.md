# RTPStatistics Entity Explanation

The `RTPStatistics` class is an analytics entity used to monitor the "Return to Player" performance of games and users.

## 📦 Properties

### Scope (Polymorphic)
- **`Id`** (`Guid`): Unique ID.
- **`GameId`** (`Guid?`): Populated if tracking a specific game's performance.
- **`UserId`** (`Guid?`): Populated if tracking a specific user's luck/skill.

### Metrics
- **`TotalWagered`** (`decimal`): Cumulative bets.
- **`TotalPaid`** (`decimal`): Cumulative wins.
- **`TotalRounds`** (`long`): Number of events.
- **`CurrentRTP`** (`double`): Computed property: `TotalPaid / TotalWagered`.
    - Used by `RtpEngine` to detect anomalies (e.g., a game paying 150% over the long run).
- **`LastCalculated`** (`DateTime`): Timestamp of the last update.