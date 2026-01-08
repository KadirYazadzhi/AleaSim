# System Audit Phase 2: Domain & Engine Deep Dive
Date: 08 January 2026
Status: Critical Issues Detected

This document covers issues found in the core Domain logic, Game Engines, and Service orchestration.

## 1. Concurrency & Data Integrity

### A. SlotGameEngine Session Lock [FIXED-PENDING-COMMIT]
*   **File:** `AleaSim.Domain/Services/SlotGameEngine.cs`
*   **Issue:** `ResolveRound` used `_cache` without locking, allowing race conditions (double-spins).
*   **Status:** **Patched.** Added `ConcurrentDictionary<Guid, SemaphoreSlim>` for per-session locking.

### B. VaultService Balance Integrity [FIXED-PENDING-COMMIT]
*   **File:** `AleaSim.Domain/Services/VaultService.cs`
*   **Issue:** `ProcessBet` and `ProcessWin` lacked synchronization, leading to potential "Lost Update" of user balance.
*   **Status:** **Patched.** Added `lock` mechanism (as MVP substitute for DB Transactions).

### C. BrainService Pre-Calculation Race
*   **File:** `AleaSim.Domain/Services/BrainService.cs`
*   **Method:** `GetNextDirective`
*   **Issue:** While it uses `lock (queue)`, if the queue is empty, it calculates 5 steps. If `DecideOutcome` is slow or relies on mutable state, multiple threads might enter the calculation phase if not careful.
*   **Fix:** Ensure the calculation block is inside the lock (already done, but needs verification for performance).

## 2. Game Logic & Security Exploits

### A. The "Negative Bet" Balance Exploit [FIXED-PENDING-COMMIT]
*   **File:** `AleaSim.Domain/Services/GameDirector.cs`
*   **Issue:** No validation for `amount > 0`. Users could send negative bets to increase their balance (`Balance -= -100` -> `Balance += 100`).
*   **Status:** **Fixed.** Added `if (amount <= 0) throw ...` in `GameDirector`.

### B. SlotGameEngine: Inconsistent Jackpot resets
*   **File:** `AleaSim.Domain/Services/SlotGameEngine.cs`
*   **Method:** `PlayBonusRound`
*   **Issue:**
    ```csharp
    bell.Value = JackpotService.ClaimJackpot(JackpotTier.Spades, repo);
    ```
    This claims the jackpot *during* the generation of the grid. If the user never finishes the bonus round (e.g., closes the tab), the jackpot is already reset in the DB, but the player never got the money (it's only in `session.GameState`).
*   **Fix:** Jackpot should be *calculated* (frozen) when the bell lands, but only *Claimed* (reset in DB and credited to user) when the Bonus Round officially finishes and `totalWin` is processed.

### C. RouletteEngine: Predictable RNG (Nonce Reuse)
*   **File:** `AleaSim.Domain/Services/RouletteGameEngine.cs`
*   **Issue:**
    ```csharp
    int nonce = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
    ```
    If two requests happen in the same millisecond (or very close), they might get the same `nonce`. Since the `serverSeed` is static for the session, they get the same result.
*   **Fix:** Store and increment a `Nonce` in the `GameSession` entity/state.

## 3. Unimplemented / "Dead" Logic

### A. AdminService: ForceCooldown
*   **File:** `AleaSim.Domain/Services/AdminService.cs`
*   **Issue:** Method is empty. Admin "Cooldown" button does nothing.
*   **Fix:** Add `LockoutUntil` to `User` or `PlayerProfile` and check it in `AuthController` or `GameDirector`.

### B. SlotGameEngine: Symbol Affinity Tracking
*   **File:** `AleaSim.Domain/Services/BrainService.cs`
*   **Method:** `UpdateProfile`
*   **Issue:** Cut off/Incomplete: `affinity[favoriteCandidate] += (dou`.
*   **Status:** **Bug.** Half-written code.

### C. AdminDashboard: RTP Trend
*   **File:** `AleaSim.Persistence/EfGameRepository.cs`
*   **Method:** `GetRtpTrend`
*   **Issue:** Not fully seen, but `AdminController` uses it. Need to ensure it's not a placeholder.

## 4. Summary of Planned Actions
1.  **Commit Phase 2 Fixes:** (Vault, GameDirector, Slot Lock).
2.  **Fix Slot Jackpot Claiming:** Move `ClaimJackpot` call to end of bonus.
3.  **Fix Roulette Nonce:** Use incremental nonce from session.
4.  **Complete BrainService:** Fix the truncated `UpdateProfile` method.
5.  **Implement ForceCooldown:** Add DB field and logic.