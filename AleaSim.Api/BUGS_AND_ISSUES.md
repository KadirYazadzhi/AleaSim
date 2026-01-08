# System Audit Phase 2: Domain & Engine Deep Dive
Date: 08 January 2026
Status: Resolved

This document covers issues found in the core Domain logic, Game Engines, and Service orchestration.

## 1. Concurrency & Data Integrity

### A. SlotGameEngine Session Lock [FIXED]
*   **File:** `AleaSim.Domain/Services/SlotGameEngine.cs`
*   **Issue:** `ResolveRound` used `_cache` without locking, allowing race conditions (double-spins).
*   **Status:** **Fixed.** Added `ConcurrentDictionary<Guid, SemaphoreSlim>` for per-session locking.

### B. VaultService Balance Integrity [FIXED]
*   **File:** `AleaSim.Domain/Services/VaultService.cs`
*   **Issue:** `ProcessBet` and `ProcessWin` lacked synchronization, leading to potential "Lost Update" of user balance.
*   **Status:** **Fixed.** Added `lock` mechanism.

### C. BrainService Pre-Calculation Race [VERIFIED]
*   **File:** `AleaSim.Domain/Services/BrainService.cs`
*   **Status:** **Verified.** The calculation block is inside the lock.

## 2. Game Logic & Security Exploits

### A. The "Negative Bet" Balance Exploit [FIXED]
*   **File:** `AleaSim.Domain/Services/GameDirector.cs`
*   **Issue:** No validation for `amount > 0`. Users could send negative bets to increase their balance.
*   **Status:** **Fixed.** Added validation in `GameDirector`.

### B. SlotGameEngine: Inconsistent Jackpot resets [FIXED]
*   **File:** `AleaSim.Domain/Services/SlotGameEngine.cs`
*   **Issue:** Jackpot was claimed during grid generation instead of round conclusion.
*   **Status:** **Fixed.** Moved `ClaimJackpot` to the end of the bonus round processing.

### C. RouletteEngine: Predictable RNG (Nonce Reuse) [FIXED]
*   **File:** `AleaSim.Domain/Services/RouletteGameEngine.cs`
*   **Issue:** Nonce was based on Ticks, leading to predictability under rapid requests.
*   **Status:** **Fixed.** Implemented persistent `RouletteState` with incremental nonce.

## 3. Unimplemented / "Dead" Logic

### A. AdminService: ForceCooldown [FIXED]
*   **File:** `AleaSim.Domain/Services/AdminService.cs`
*   **Issue:** Method was empty.
*   **Status:** **Fixed.** Added `LockoutUntil` to `User` and logic in `AdminService` and `GameDirector`.

### B. SlotGameEngine: Symbol Affinity Tracking [FIXED]
*   **File:** `AleaSim.Domain/Services/BrainService.cs`
*   **Issue:** Code was truncated/broken.
*   **Status:** **Fixed.** Cleaned up and completed the `UpdateProfile` method.

### C. AdminDashboard: RTP Trend [VERIFIED]
*   **Status:** **Verified.** Implementation exists in `EfGameRepository`.

## 4. Summary
The system is now robust against concurrency issues, common betting exploits, and RNG predictability. All core "Admin Control" features (including Cooldown) are now functional.
