# ⚙️ Technical System Specifications

This document defines the technical behavior and constraints of the active AleaSim platform.

## 1. Game Mechanics: Clover Chase (Slot)

### 💎 Special Bells (Jackpot Tiering)
*   **Mini Jackpot:** Fixed at 1000x Denomination. Probability is high but decays exponentially as more land on screen.
*   **Minor Jackpot:** Fixed at 5000x Denomination. Hard-capped at 3 per bonus game.
*   **Major Jackpot:** Linked to the global `JackpotPool` (Spades tier). Distributed lock prevents double-claiming.

### 🃏 Gamble Feature
*   **Availability:** Offered after every standard win (Line win or instant coin win).
*   **Logic:** Double-or-Nothing (50/50). Uses a dedicated cryptographic nonce to ensure fairness.
*   **Security:** Participation state is saved in Redis to prevent "refresh-to-retry" cheats.

### 📏 Denomination Control
*   **Mechanism:** Bets are calculated as `Credits * Denomination`. 
*   **Supported:** $0.01, $0.02, $0.05, $0.10, $0.20, $0.50, $1.00.
*   **Validation:** Denomination is locked during active Respins/Bonus rounds to prevent betting strategy exploitation.

## 2. The Brain (Behavioral Engine)

### 🎯 Engagement Optimization
*   **Flow State:** The system tracks `AvgSpinInterval`. Volatility is dynamically adjusted:
    *   *Fast Play:* Increases win magnitude but reduces frequency.
    *   *Slow Play:* Swaps to "Popcorn Mode" (frequent small hits) to re-engage interest.
*   **Retention Hooks:** If `LossStreak > 8`, the Brain requests a "RetentionHook" directive from the CMS to deliver a recover-win (approx 15x-20x bet).

### 🛠️ Global Control
*   **Shadow Mode:** When enabled, the platform operates on pure RNG, disabling all behavioral intervention for all users.
*   **Forced Directives:** Admin-level overrides expire after 10 minutes to ensure no permanent account manipulation occurs.

## 3. The Vault (Financial Guard)

### 🏦 Shadow Accounting
*   **RTP Enforcement:** Every user has a virtual "bankable" pool. If a win exceeds this pool and the `GlobalPool` is low, the Vault denies the payout.
*   **Atomic Increment:** All balance and pool updates use raw SQL atomic increments (`UPDATE ... SET val = val + x`) to eliminate race conditions.
*   **Daily Loss Limit:** A hard-stop mechanism that blocks betting if the current day's net loss exceeds the user's configured limit.

## 4. Automation & Trust

### 🔐 Provably Fair
*   **Algorithm:** HMAC-SHA256.
*   **Parameters:** `ServerSeed` (Hidden), `ClientSeed` (User), `Nonce` (Round Counter + Attempt Offset).
*   **Verification:** Users can rotate their seed at any time to reveal the old one and verify all past outcomes using the built-in Verifier UI.

### 📡 Real-Time Reliability
*   **Online Presence:** Redis-backed presence tracking ensures accurate "Active Player" counts.
*   **State Recovery:** Every game engine implements `GetCurrentState` to allow instant UI reconstruction after browser refresh or disconnect.
