# 🏆 Promotions & Competitive Ecosystem

AleaSim utilizes automated social and competitive layers to maximize player retention and platform turnover.

---

## 1. The ROI Tournament (Fair Competition)
Unlike standard total-win leaderboards, AleaSim uses an **ROI (Return on Investment)** ranking model.

*   **Formula:** `((Total Payout - Total Wagered) / Total Wagered) * 100`.
*   **Period:** Full Calendar Month (Aggregation starts on the 1st at 00:00:00).
*   **Payout:** Automated by the `TournamentPayoutWorker` on the **1st of the next month**.
*   **Prize Pool:** 
    *   **Base:** $25,000.00
    *   **Dynamic:** +1% of every bet placed on the platform during the month.
*   **Distribution:** 1st place (40%), 2nd place (25%), 3rd place (15%), 4th-10th (shared 20%).

## 2. Raffle Mechanics (Random Drops)
Raffles provide excitement through unpredictable "Cash Drops" from the global pool.

### Eligibility
*   **Active Criteria:** Must have placed a bet within **3 minutes** of the draw.
*   **Tickets:** 1 Ticket per $50.00 of monthly turnover.
*   **Re-roll:** If an inactive user is picked, the system performs a recursive re-roll until an active player is found.

### Frequency
*   **Weekly:** Every Sunday evening.
*   **Monthly:** Last day of the month.

## 3. The Wallet Flow (Bonus Funds)
All promotional rewards (Raffles, Tournaments, Daily Streak) enter the **Bonus Wallet**.

*   **Usage:** Bonus funds are consumed *before* real balance.
*   **Wagering:** 1x Wagering requirement. 
*   **Conversion:** Once the total wagered amount equals the awarded bonus, the remaining bonus balance is converted to real cash.

## 4. Daily Retention Incentives
*   **Bonus Wheel:** Available every 24h. Offers random cash prizes or XP boosts.
*   **Daily Streak:** Increasing rewards for consecutive days played (capped at 50 units).
*   **Cashback:** Accrued in real-time based on VIP level and loss volume.
