# ITransaction Interface Explanation

The `ITransaction` interface wraps the underlying database transaction object (e.g., `IDbContextTransaction` in EF Core).

## 🎯 Purpose
To prevent "Leaky Abstractions". By returning this interface instead of the EF Core transaction object directly, the Domain layer remains independent of Entity Framework.

## 🛠️ Method Contracts
- **`Commit()`**: Persists all operations permanently.
- **`Rollback()`**: Reverts all operations if an error occurs.
- **`IDisposable`**: Ensures the transaction resources are released (connection closed) automatically via `using` blocks.
