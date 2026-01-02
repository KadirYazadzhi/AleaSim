# 🚧 Remaining Features & Future Roadmap

This document outlines the features that are architecturally planned but not yet implemented in the codebase. It serves as the "To-Do" list for moving from Prototype to Production.

---

## 1. Automation (The Heartbeat)
*Current Status:* Logic exists but must be triggered manually via API.
*Missing:* Background Workers (Cron Jobs).

### Required Components:
*   **`RaffleScheduler` (Background Service):**
    *   Runs continuously.
    *   Checks: "Is it Sunday 19:00 - 21:00?".
    *   Action: Triggers `PromotionService.ExecuteRaffleDraw` at random intervals during the window.
*   **`DailyBonusJob` (Daily Cron):**
    *   Runs at 00:01 every day.
    *   Calculates previous day's P/L for every user.
    *   Distributes Cashback or Loyalty bonuses.

---

## 2. Tournament Logic (The Leaderboard)
*Current Status:* Math for multipliers exists, but no ranking system.
*Missing:* Live Ranking & Payout Automation.

### Required Components:
*   **`LeaderboardService`:**
    *   Ideally uses Redis (Sorted Sets) for high-performance ranking.
    *   Tracks `MaxMultiplier` for every user on the 30th of the month.
*   **`TournamentEnder`:**
    *   Runs at 23:59 on the 30th.
    *   Takes Top 10 users.
    *   Credits prizes to their Bonus Wallet.

---

## 3. Bonus Lifecycle & Cashout Options
*Current Status:* Basic "Sticky Bonus" implemented.
*Missing:* User options to forfeit/convert bonus.

### New Feature: "Instant Cashout" (The 1/10 Rule)
A user can choose to bypass the wagering requirement by taking a drastic "haircut" on the value.

*   **Scenario:** User has 500 Bonus Credits.
*   **Option A (Play):** Wager the full amount (1x). Keep winnings.
*   **Option B (Cashout):** Convert immediately to Real Cash at **10% value**.
    *   Result: 50 Real Cash added. 500 Bonus removed.
*   **Constraint:** Only available if `BonusAmount >= 100`. If `< 100`, the user can forfeit (remove) the bonus, but gets $0 real cash (it just disappears).

---

## 4. Daily Cashback & Loyalty (Retention System)
*Missing:* Logic to calculate and award these daily.

### Rules:
*   **The Loser's Comfort (Cashback):**
    *   If `Yesterday_Net_Result < 0` (Loss).
    *   Award: **10% of Loss** as Bonus.
*   **The Winner's Perk (Loyalty):**
    *   If `Yesterday_Net_Result > 0` (Win).
    *   Award: **5% of Profit** as Bonus.
*   *Note:* These bonuses are subject to the same "Instant Cashout" rules above.

---

## 5. Advanced Slot Math (Multi-Line Construction)
*Current Status:* Basic single-symbol matching.
*Missing:* Complex algorithms to build exact win amounts using multiple paylines.

### The Problem:
If The Brain demands a win of **12.50**, but symbols only pay 10 or 15, the current system fails or rounds down.

### The Solution:
*   **`MultiLineSolver`:** An algorithm that solves the equation:
    *   `Line1_Pay + Line2_Pay + ... + LineN_Pay = TargetTotal`
    *   Example: Line 1 (10.00) + Line 5 (2.50) = 12.50.
    *   This requires manipulating the reel strips to satisfy multiple geometric constraints simultaneously.

---

## 6. Admin Panel API
*Current Status:* None. Configuration is hardcoded.
*Missing:* Endpoints to tweak The Brain.

### Required Endpoints:
*   `POST /admin/config/rtp`: Set Global Target RTP (e.g., change from 95% to 92%).
*   `POST /admin/config/brain`: Adjust "Retention Aggression" (how often Brain forces wins).
*   `GET /admin/players/{id}/profile`: View the full dossier (LTV, Churn Score, pRTP).
