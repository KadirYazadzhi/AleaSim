# IGameRepository Interface Explanation

The `IGameRepository` interface is a comprehensive contract for the Data Access Layer. It abstracts all database interactions, allowing the Domain layer to remain agnostic of the underlying storage technology (PostgreSQL, SQL Server, etc.).

## 🛠️ Functional Groups

### Transaction Management
- **`BeginTransaction`** (`ITransaction`): Starts an atomic unit of work. Essential for operations that span multiple tables (e.g., deducting balance AND saving a bet).
- **`SaveChanges`**: Commits pending changes to the database.

### Core Entities
- **Sessions**: `CreateSession`, `GetSession`, `EndSession` manage the lifecycle of player interactions.
- **Users**: `GetUser`, `CreateUser`, `UpdateUserBalance` manage identity and the critical wallet operations.
- **Bets & Rounds**: Methods to persist the high-volume gameplay data (`SaveBet`, `SaveRound`).

### Statistics & Jackpots
- **RTP**: `GetOrCreateGameStats`, `UpdateRtpStats` allow the engine to monitor and update performance metrics.
- **Jackpots**: `GetGlobalJackpot`, `UpdateJackpot` handle the shared prize pools.

### Security
- **Audit**: `LogAudit`, `GetLastAuditHash`.
    - **Note**: `GetLastAuditHash` is crucial for the blockchain-integrity check, allowing the new log to link to the previous one in the DB.
