# 🏆 Promotions & Perpetual Competitive Ecosystem (v2.0)

AleaSim utilizes an automated social and competitive layer that runs 24/7 without manual operator intervention.

---

## 1. The ROI Tournament (Fair Competition)
Unlike standard "total-win" leaderboards which favor high rollers, AleaSim uses an **ROI (Return on Investment)** ranking model, giving all players a fair chance.

*   **Formula:** `((Total Payout - Total Wagered) / Total Wagered) * 100`.
*   **Perpetual Cycles:** The system automatically rotates seasons on the 1st of every month.
*   **Rollover Pool:** If no one qualifies during a season, the $25,000 base prize pool rolls over to the next month.
*   **Distribution:** 1st place (40%), 2nd place (25%), 3rd place (15%), 4th-10th (shared 20%).

## 2. Raffle Mechanics (Lucky Drops)
Raffles provide real-time excitement through unpredictable "Cash Drops" from the global platform pool.

### Eligibility
*   **Active Criteria:** Must have placed a bet within **3 minutes** of the draw (monitored via Redis presence).
*   **Recursive Re-roll:** If an inactive or excluded user is picked, the `RaffleWorker` immediately performs a recursive re-draw until an active player is found.

## 3. The Industrial Wallet Flow
Rewards are credited to the **Bonus Wallet** with an industrial safeguarding process:

*   **Idempotency:** Every reward grant uses a unique reference ID.
*   **Usage:** Bonus funds are consumed *before* real balance.
*   **Conversion:** Automatic conversion to real cash once the 1x wagering requirement is met.

## 4. Daily Retention Incentives
*   **Bonus Wheel (24h):** Fully responsive full-screen wheel (100% width on mobile) offering cash or XP.
*   **Must Drop Jackpots:** Progressive pools with a hard cap that **must trigger** before a specific value, generating intense user FOMO.
*   **Global Notifications:** "Big Win" events are broadcasted globally via SignalR to validate platform payouts to all connected users.
