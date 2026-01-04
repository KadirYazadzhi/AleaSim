# 🐛 Detailed Bug Report & System Deficiencies Analysis

This document outlines the critical issues identified in the current implementation of AleaSim, categorized by component. It serves as a roadmap for immediate remediation.

---

## 1. Game Mechanics: Clover Chase (Slot)

### ❌ Static Jackpots (No Progressive Growth)
*   **The Issue:** The Mini/Minor jackpots in the Bonus Game are calculated as static multipliers (`Denomination * Constant`). They do not grow based on player wagers.
*   **Root Cause:** `SlotGameEngine.cs` uses hardcoded math instead of querying `JackpotService` for the current accrued pool value.
*   **Impact:** Players have no incentive to play more to grow the pot. It breaks the "Progressive" promise.

### ❌ Missing "Local" Jackpot Logic
*   **The Issue:** The Major/Mega jackpots are treated as generic high-value wins. They should be "Local" progressives specific to Clover Chase, growing only from bets on this game.
*   **Root Cause:** The database schema has `Jackpots` table, but the Engine logic ignores the `GameId` link and doesn't fetch specific game pools.

---

## 2. Game Mechanics: Roulette Royale

### ❌ The "Zero Win" Bug
*   **The Issue:** The game almost always returns 0 or a losing number.
*   **Root Cause:** The Reverse Engineering logic in `RouletteGameEngine.cs`. When the Brain sends a "Random" directive (which has `TargetWin = 0` by default), the engine interprets this strictly as "The player MUST win $0". It actively finds a losing number instead of letting RNG decide a fair outcome.
*   **Impact:** The game feels rigged and unfair in standard play.

---

## 3. Game Mechanics: Blackjack

### ❌ Balance Desynchronization
*   **The Issue:** The player's balance does not update visibly during "Hit/Stand" actions. It only updates abruptly at the end of a session or on refresh.
*   **Root Cause:** The frontend `Blackjack.razor` only fetches the user balance on `OnInitialized`. It does not listen to real-time balance updates or refresh the profile after intermediate actions.
*   **Impact:** Players feel like they are playing a free demo or that wins aren't paying out.

---

## 4. The Admin Panel (Backoffice)

### ❌ Lack of Separation
*   **The Issue:** The Admin Panel is just a set of extra pages inside the main Player App (`MainLayout`). It looks and feels like the casino frontend.
*   **Requirement:** A completely separate `AdminLayout` with a professional dashboard look (Side navigation, high-density data grids, dark/data-centric theme).

### ❌ Missing "Live Monitor"
*   **The Issue:** Admins cannot see who is playing *right now*.
*   **Requirement:** A real-time stream (SignalR) showing: `[Time] [User] [Game] [Action] [Bet] [Win]`.

### ❌ Incomplete God Mode
*   **The Issue:** Controls are limited to a single player profile.
*   **Requirement:** A global control center to:
    *   Change System-wide RTP instantly.
    *   Inject/Deduct balance manually.
    *   Force specific Brain Modes (e.g., "Happy Hour" - boost luck for everyone).

### ❌ Missing Reports
*   **The Issue:** No historical data aggregation.
*   **Requirement:** Pages for "24h Financials", "Jackpot History", "Top Winners", and "RTP Deviation Report".

---

## 5. The Brain & Vault

### ❌ Loose Shadow Wallet
*   **The Issue:** While `ShadowBalance` property exists, the enforcement is loose. A player can technically win more than their shadow balance if the global pool allows it, blurring the lines of "Personal RTP".
*   **Requirement:** Strict enforcement logic in `VaultService` that prioritizes Shadow Wallet for standard wins and only touches Global Pool for Jackpots.

### ❌ Brain Responsiveness
*   **The Issue:** Changing Brain settings (e.g., via Admin) might not affect the current session immediately due to caching or lack of signal.
*   **Requirement:** Real-time config injection into the active `BrainService`.

---

## 6. Frontend / UX

### ❌ Visual Disconnect
*   **The Issue:** Animations (Spinning) are simple CSS classes. They don't have the "physics" feel of a real slot.
*   **Requirement:** Implementation of more robust animation libraries or Canvas-based rendering (PixiJS) for the reels.

### ❌ No Credits View
*   **The Issue:** While Denomination logic exists in backend, the UI mostly shows Raw Currency.
*   **Requirement:** Toggle to switch view between "Cash ($)" and "Credits (Coins)".
