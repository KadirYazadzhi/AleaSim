# EfTransactionWrapper Implementation Explanation

`EfTransactionWrapper.cs` acts as an Adapter pattern between the Domain layer and EF Core.

## 🎯 Purpose
The Domain layer (Services) defines `ITransaction` but doesn't know what Entity Framework is. This wrapper hides the `IDbContextTransaction` object.

## 🛠️ Usage
- **Commit**: Flushes the transaction to the database.
- **Rollback**: Cancels it.
- **Dispose**: Closes the database connection associated with the transaction.
