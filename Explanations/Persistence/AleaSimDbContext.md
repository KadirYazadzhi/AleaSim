# AleaSimDbContext Explanation

The `DbContext` definition.

## 📦 DbSets
Exposes all Domain entities as queryable sets.
- `AuditLogs`
- `RTPStatistics`
- `Jackpots`
- `Outcomes`
- `GameRounds`
- `Bets`
- `GameSessions`
- `Games`
- `Users`

## ⚙️ Configuration
Uses `ApplyConfigurationsFromAssembly` to automatically load all the separate `*Configuration.cs` files.