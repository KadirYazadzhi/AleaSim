# 🏆 Promotions, Raffles & Tournaments

## Overview
To maximize player retention and create peak traffic periods, the system implements three types of automated promotional events: **Weekly Raffles**, **Monthly Raffles**, and **The Monthly Multiplier Tournament**.

---

## 1. Eligibility: The "Active Player" Rule
A player is considered **ELIGIBLE** for a prize drop only if they meet the "Active" criteria:
*   **Active Status:** Must have placed at least one bet of any amount within the **last 3 minutes** prior to the prize draw.
*   **Rationale:** This ensures that prizes are awarded to players currently engaged with the games, rather than idle sessions or bot accounts.

---

## 2. Raffle Mechanics (Weekly & Monthly)

### Tickets & Probability
*   **Ticket Generation:** 1 Ticket is awarded for every **50 units** of total turnover (wagered amount).
*   **Weighted Random:** The draw uses a weighted probability model. A player with 15,000 tickets has a much higher statistical chance than a player with 1 ticket, but the outcome remains stochastic (not guaranteed).

### Scheduling & Distribution
*   **Weekly Raffle:** Every Sunday, 19:00 - 21:00. (Pool: 20,000).
*   **Monthly Raffle:** 30th of every month, 19:00 - 21:00. (Pool: 50,000).
*   **Time-Gated Drops:** Prizes are not awarded all at once. A scheduler distributes "Drop Points" randomly across the 120-minute window.
*   **Randomized Payout Order:** The sequence of prize amounts (e.g., 500, 5000, 1000) is shuffled. Players never know when the "Big One" will drop.
*   **The Re-roll Logic:** If the system draws a winner who is currently **OFFLINE** or **INACTIVE** (no bet in 3 mins), the system immediately performs a new draw for the same amount until an active player is found.

---

## 3. Monthly Multiplier Tournament

*   **Duration:** 24 Hours on the 30th of every month (00:00 - 23:59).
*   **Goal:** Highest **Profit Percentage Difference** (Multiplier).
    *   *Formula:* `(Total Win / Total Bet) * 100`.
    *   *Benefit:* Allows low-stakes players to compete fairly against high-rollers.
*   **Leaderboard:** Top 10 players share a **20,000 pool** in descending order.
*   **Payout:** Automatically credited at 00:01 on the 1st of the next month.

---

## 4. The Wallet System: Locked Bonus Logic

Prizes from Raffles and Tournaments are not credited as "Raw Cash". They enter the **Bonus Wallet** with specific rules:

### Funds Hierarchy
1.  **Bonus First:** When a player has a bonus balance, the system automatically uses **Bonus Funds** for bets instead of the Real Balance.
2.  **Real Balance Freeze:** The player's original capital remains untouched while the bonus is active.

### Wagering Requirement (The 1x Rule)
*   **Requirement:** 1x Wagering. The player must place total bets equal to the prize amount received.
*   **Conversion:** Once the total bets reach the prize value, the remaining balance in the Bonus Wallet is converted and moved to the **Real Balance**.
*   **Restrictions:** Players cannot withdraw or use the prize for non-game actions until the 1x wagering is complete.

---

## 5. Technical Implementation Notes
*   **Scheduler:** Requires a background worker that pre-calculates drop timestamps at the start of the event.
*   **Activity Tracker:** A high-speed cache (e.g., Redis) should store the `LastBetTimestamp` for every user to allow near-instant "Active" checks during draws.
*   **Leaderboard Engine:** A real-time scoring system to track multipliers without heavy DB queries.
