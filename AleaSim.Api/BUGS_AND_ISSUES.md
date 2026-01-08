# System Audit: AleaSim.Api
Date: 08 January 2026
Status: In Progress

This document outlines the bugs, security vulnerabilities, architectural flaws, and performance bottlenecks identified during the initial code review of the `AleaSim.Api` project.

## 1. Critical Logic Errors & Broken Features

### A. Sentinel Service "Blind Spot" [FIXED]
*   **File:** `Workers/SentinelBackgroundService.cs`
*   **Method:** `ScanForAnomalies`
*   **Issue:** The service calls `repo.GetUserRounds(Guid.Empty, 100)`. Passing `Guid.Empty` usually signifies a filter for a specific user ID. If the repository logic filters strictly by ID, this query returns nothing (or only system records).
*   **Consequence:** The security monitor is effectively blind and checks zero player bets.
*   **Fix:** Implement a method `repo.GetGlobalRecentRounds(int count)` to fetch the latest bets regardless of user ID.
*   **Status:** **Fixed.** Added `GetGlobalRecentRounds` to repository and updated service to use it.

### B. Missing Bot Detection Logic
*   **File:** `Workers/SentinelBackgroundService.cs`
*   **Method:** `ScanForAnomalies`
*   **Issue:** The comment mentions "Bot Detection (Rapid betting)", but the implementation is just a log statement: `_logger.LogDebug(...)`.
*   **Consequence:** No protection against script/bot attacks.

### C. Admin Dashboard "Fake" Data [FIXED]
*   **File:** `Controllers/AdminController.cs`
*   **Method:** `GetActiveSessions`
*   **Issue:** Explicitly returns `new List<string>()` with a comment `// Placeholder fix`.
*   **Consequence:** Administrators cannot see or manage active player sessions.
*   **Fix:** Added `GetActiveSessionsDetails` to repository returning `ActiveSessionDto` with joins. Updated `AdminController` to use it.

## 2. Security Vulnerabilities

### A. Hardcoded Admin Backdoor [FIXED]
*   **File:** `Controllers/AuthController.cs`
*   **Method:** `PromoteToAdmin`
*   **Issue:** Contains `if (secret != "MakeMeAdminPlease")`.
*   **Risk:** **CRITICAL**. Any user who knows this string (visible in source control) can elevate privileges to Admin.
*   **Fix:** Move secret to Environment Variables/Vault and hash it.
*   **Status:** **Fixed.** Changed to use `Configuration["Admin:Secret"]`.

### B. Information Leakage (Exception Handling) [FIXED]
*   **File:** Global (Controllers)
*   **Issue:** Catch blocks return `BadRequest(ex.Message)`.
*   **Risk:** Exposes internal system details (stack traces, database timeouts, column names) to potential attackers.
*   **Fix:** Log the specific error and return a generic "Internal Server Error" or sanitary message to the client.
*   **Status:** **Fixed.** Updated Controllers to use `_logger.LogError` and return generic 500 status for unhandled exceptions.

### C. Authentication Flow Fragility [FIXED]
*   **File:** `Controllers/GameController.cs` (and others)
*   **Method:** `CashoutBonus`
*   **Issue:** `Guid.Parse(User.FindFirst(...)?.Value ?? Guid.Empty.ToString())`.
*   **Risk:** If the token claim is missing, it defaults to `Guid.Empty` instead of throwing 401 Unauthorized. This might lead to database queries executing against a non-existent "Zero User", causing unpredictable behavior.
*   **Fix:** Implemented `GetUserIdOrThrow` helper method that throws `UnauthorizedAccessException` on missing claim.
*   **Status:** **Fixed.** Applied to Auth, Game, and Vault controllers.

## 3. Performance & Architecture

### A. The "N+1" Query Disaster [FIXED]
*   **File:** `Controllers/GameController.cs`
*   **Method:** `GetHistory`
*   **Issue:** Retrieves a list of rounds, then iterates through *each* round to query the Session and then the Game individually (`repo.GetSession`, `repo.GetGame`).
*   **Impact:** For 50 history items, this executes **101 database queries**. Under load, this will crash the database.
*   **Fix:** Use `.Include(r => r.Session.Game)` in the repository or a proper SQL JOIN.
*   **Status:** **Fixed.** Implemented `GetUserHistory` in Repository with efficient Joins.

### B. Thread Safety / Race Conditions [FIXED]
*   **File:** `Workers/SentinelBackgroundService.cs`
*   **Member:** `_recentAlerts` (List<T>)
*   **Issue:** The list is modified by the background thread (`ScanForAnomalies`) and read by the API thread (`GetAlerts`) simultaneously without locking.
*   **Impact:** Will cause `System.InvalidOperationException: Collection was modified` and crash the service under load.
*   **Fix:** Use `ConcurrentQueue<T>` or a `lock` mechanism.
*   **Status:** **Fixed.** Added lock mechanism for `_recentAlerts`.

### C. Data Wipe on Restart
*   **File:** `Program.cs`
*   **Line:** `db.Database.EnsureDeleted();`
*   **Issue:** The database is completely dropped every time the application starts.
*   **Impact:** Impossible to maintain state, test long-term features, or keep user accounts between restarts.

### D. Incorrect Dependency Injection Usage [FIXED]
*   **File:** All Controllers
*   **Issue:** Injecting `IServiceScopeFactory` and manually creating scopes (`using var scope = ...`) inside controller actions.
*   **Impact:** Controllers are already Scoped. This adds unnecessary boilerplate, complexity, and slight overhead. Services like `IGameRepository` should be injected directly into the Controller constructor.
*   **Status:** **Fixed.** Refactored all Controllers to use Constructor Injection.

## 4. Missing Implementation Details

The following services are used via Interface but their logic was not found in the API layer analysis (likely in `AleaSim.Domain` or `AleaSim.Shared` or unimplemented):
*   `IPromotionService` (Crucial for `DailySpin`)
*   `ITournamentService` (Crucial for Leaderboards)
*   `IVoucherService` (Crucial for Economy)
*   `ILevelService` (Crucial for RPG progression)

These require further investigation to ensure they aren't also just "Placeholders".
