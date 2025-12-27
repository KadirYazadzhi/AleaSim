# JackpotService Implementation Explanation

`JackpotService.cs` manages the shared prize pools.

## ⚙️ Logic

### Database Persistence
- **Old Version**: In-Memory Dictionary (Lost on restart).
- **New Version**: Uses `IGameRepository` to fetch `Jackpot` entities from PostgreSQL.

### Contribution
- Updates `CurrentValue` by the configured percentage.
- **Real-Time Notification**: Calls `_realTimeService.NotifyJackpotUpdate` so all connected clients see the ticker go up immediately.

### Triggering
- **Check**: Performs a low-probability RNG roll.
- **Reset**: If won, it resets the value to the seed (500 or 10000) *inside the same lock*.
- **Atomicity**: Uses `lock (_lock)` to ensure that if 1000 users spin at once, the database update happens sequentially. *Note: In a cluster, this lock needs to be distributed (Redis/DB lock).*