# 🤖 Automation & Tournament Logic Specs

## 1. The Tournament (Strict Rules)
*   **Schedule:** Runs strictly on the **30th of every month** from **00:00:00** to **23:59:59**.
*   **Isolation:** Data from previous days (e.g., 29th) or subsequent days must NOT count.
*   **Ranking Criteria:** **ROI Percentage (Return on Investment).**
    *   It is NOT based on total absolute profit.
    *   Formula: `((TotalPayout - TotalWagered) / TotalWagered) * 100`.
    *   *Example:* Player A bets 10, wins 100. Profit = 90. ROI = 900%.
    *   *Example:* Player B bets 1000, wins 1500. Profit = 500. ROI = 50%.
    *   **Winner:** Player A (900% > 50%).
*   **Entry Threshold:** To filter out noise (e.g., 1 spin), players might need a minimum bet count (e.g., 10 spins) to qualify (Optional, but recommended).

## 2. Background Workers (The Schedulers)

### A. `RaffleWorker` (Runs continuously)
*   **Weekly Raffle:**
    *   Time: Sunday 19:00 - 21:00.
    *   Logic: Random "Drops" distributed in this window.
*   **Monthly Raffle:**
    *   Time: 30th 19:00 - 21:00.
    *   Logic: Same as weekly, larger pool.

### B. `DailyProcessingJob` (Runs at 00:01 daily)
*   **Task 1: Daily Retention Bonuses**
    *   Calculate yesterday's Net P/L (`TotalPaid - TotalWagered`).
    *   If **Loss**: Give **10% Cashback** (Bonus Wallet).
    *   If **Win**: Give **5% Loyalty Reward** (Bonus Wallet).
    *   *Constraint:* User notified they can forfeit bonus for 1/10th real cash value (if > 100).
*   **Task 2: Tournament Finalization**
    *   Check: "Was yesterday the 30th?"
    *   If Yes: Query `TournamentEntries` for Top 10 by ROI.
    *   Action: Award prizes to winners.
    *   Action: Wipe leaderboard for next month.
