# BaseGameEngine - Abstract Core

`BaseGameEngine.cs` provides the foundational plumbing for all specific game types. It implements the **Template Method Pattern**, defining the skeleton of the workflow while letting subclasses fill in the specifics.

## 🏗️ Architecture & Dependencies

It injects the three core pillars via Constructor Injection:
1.  **`IRngService`**: Source of truth for randomness.
2.  **`IRtpEngine`**: Financial guardrails.
3.  **`IJackpotService`**: Bonus system.

## 🔑 Shared Logic Implementation

### Session Management (`ActiveSessions`)
- Uses a `ConcurrentDictionary` to store active games in memory.
- **Why Concurrent?** Thread-safe access allows multiple HTTP requests to read/write session data without crashing.

### Balance Simulation (`UserBalances`)
- **Note**: In a real production app, this would be a Database Call (SQL `UPDATE Users SET Balance...`).
- **Here**: It uses an in-memory dictionary to simulate a wallet.
- **Default Balance**: If a user is new, it gives them `1000m` automatically so the simulation is playable immediately.

### Betting Flow (`PlaceBet`)
This method handles the universal rules before the game logic even starts:
1.  **Validation**: Is the session real? Is amount > 0?
2.  **Funds Check**: Do they have money?
3.  **Deduction**: `Balance -= Amount`.
4.  **Recording**: Calls `RtpEngine.RecordBet` and `JackpotService.Contribute`.

## 🧩 Abstract Methods
Subclasses *must* implement:
- `ResolveRound`: The actual game rules (Cards, Slots, etc.).
- `GetOutcome`: Formatting the result.
