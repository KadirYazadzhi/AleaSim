# BaseGameEngine Implementation Explanation

`BaseGameEngine.cs` is the abstract foundation for all specific game logic (Slots, Roulette, etc.). It manages the common lifecycle of betting, session management, and database transaction scopes.

## 🏗️ Architecture

### Scope Management (`ExecuteScoped`)
Since game engines might be long-lived or accessed via SignalR (outside a standard HTTP request context), they cannot simply inject a `DbContext`.
- **Pattern**: It injects `IServiceScopeFactory`.
- **Logic**: Every time it needs to touch the database, it creates a *new* scope (`using var scope = ...`), resolves the `IGameRepository`, does the work, and disposes of the scope. This prevents "DbContext has been disposed" errors.

### Transactional Betting (`PlaceBet`)
1.  **Validation**: Checks active session and user balance.
2.  **Atomic Transaction**:
    - Deducts Balance (`UpdateUserBalance`).
    - Creates `Bet` record (`SaveBet`).
    - Updates RTP Stats (`RtpEngine.RecordBet`).
    - Updates Jackpot (`JackpotService.Contribute`).
    - **Commit**: If any step fails, *everything* rolls back. The user doesn't lose money if the bet fails to save.

### Automatic Game Registration
- **`GetGameId`**: Automatically checks if the game type (e.g., "Slot") exists in the DB. If not, it creates it. This allows "Code-First" game deployment.