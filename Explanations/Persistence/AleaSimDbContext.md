# AleaSimDbContext - Database Context

`AleaSimDbContext` is the heart of the persistence layer. It represents a session with the database and acts as a unit of work.

## 🎯 Purpose
1.  **Entity Mapping**: Tells EF Core which C# classes correspond to database tables.
2.  **Querying**: Provides `DbSet<T>` properties (like `Users`, `Bets`) that allow LINQ queries to be translated into SQL.
3.  **Transaction Management**: `SaveChanges()` wraps all changes in a transaction, ensuring data consistency (ACID).

## 🏗️ DbSet Properties
These properties expose the tables:
- `Users`: Stores player accounts.
- `Games` & `GameSessions`: Catalog and state tracking.
- `Bets`, `GameRounds`, `Outcomes`: The core gameplay loop data.
- `Jackpots`: Financial pools.
- `RTPStatistics`: Analytics.
- `AuditLogs`: Security trail.

## ⚙️ Configuration Loading (`OnModelCreating`)
```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(AleaSimDbContext).Assembly);
```
- **Pattern**: Instead of writing hundreds of lines of configuration (e.g., `builder.Entity<User>().HasKey...`) inside this method, it automatically scans the assembly for classes implementing `IEntityTypeConfiguration<T>`.
- **Benefit**: Keeps the `DbContext` clean and separates the schema definition logic into dedicated files in the `Configurations` folder.
