# AuditService Implementation Explanation

`AuditService.cs` implements the secure logging requirements defined in `IAuditService`.

## ⚙️ Key Implementation Details

### Database Interaction
- **Dependency**: Uses `IServiceScopeFactory` to create a fresh scope and resolve `IGameRepository`. This is necessary because `AuditService` might be a Singleton, while the repository is Scoped (per-request).
- **Persistence**: Unlike the previous in-memory list, logs are now saved directly to the database via `repo.LogAudit()`.

### The Blockchain Logic
- **State**: Maintains a `_lastHash` variable in memory.
- **Initialization**: On startup, it queries `repo.GetLastAuditHash()` to find the end of the current chain.
- **Locking**: Uses `lock (this)` to ensure that if multiple threads try to log an event, they happen sequentially. This is crucial so that the `PreviousHash` of event B is exactly the `Hash` of event A.

### Hashing Strategy
- **Algorithm**: SHA-256.
- **Input**: `Timestamp` + `EventType` + `UserId` + `Metadata` + `PreviousHash`.
- **Result**: A change in *any* of these fields completely changes the output hash.