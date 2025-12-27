# EfGameRepository Implementation Explanation

`EfGameRepository.cs` is the **Persistence Hub**. It implements `IGameRepository` using Entity Framework Core.

## ⚙️ Key Implementation Details

### Initialization (Seed-on-Read)
The repository uses a "Lazy Initialization" strategy for several entities:
- **Jackpots**: `GetGlobalJackpot()` checks if a record exists. If not (first run), it creates the default "Global Grand Jackpot" with $10,000.
- **RTP Stats**: `GetOrCreateGameStats` ensures a stats record always exists for every game/user pair, avoiding null checks in the engine.
- **Games**: `GetGameByType` is used by the engines to self-register.

### Transaction Support
- **`BeginTransaction`**: Wraps the EF Core transaction in a `EfTransactionWrapper`.
- **Atomic Operations**: Methods like `CreateSession` or `SaveBet` call `SaveChanges()` individually. This is fine for single operations. However, for complex flows (like `ResolveRound` in engines), these methods are called inside a `BeginTransaction` scope, which defers the actual commit until the wrapper's `Commit()` is called.

### Data Access Patterns
- **User Lookups**: By ID or Username.
- **Audit Logging**: Appends to the `AuditLogs` DbSet.
- **State Retrieval**: `GetLastRound` uses `OrderByDescending(Timestamp).FirstOrDefault()` to recover the state of stateful games like Blackjack.
