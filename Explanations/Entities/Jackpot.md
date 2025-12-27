# Jackpot Entity Explanation

The `Jackpot` class represents a progressive prize pool that accumulates value over time based on user activity.

## 📦 Properties

### Identity
- **`Id`** (`Guid`): Unique ID.
- **`Name`** (`string`): Display name (e.g., "Grand Jackpot").

### Economics
- **`CurrentValue`** (`decimal`): The specific amount currently available to be won. Updated in real-time.
- **`ContributionRate`** (`decimal`): The fractional percentage of every bet that feeds this pool (e.g., 0.01 for 1%).

### Scope
- **`IsGlobal`** (`bool`):
    - `true`: Fed by all games across the platform.
    - `false`: Tied to a specific game.
- **`GameId`** (`Guid?`): If not global, links to the specific `Game` entity.
- **`LastUpdated`** (`DateTime`): Used for concurrency control and freshness checks.
