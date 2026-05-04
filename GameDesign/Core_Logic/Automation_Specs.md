# 🤖 Automation & Perpetual Systems (v2.0)

AleaSim utilizes an advanced automation layer to maintain platform integrity, manage competitive seasons, and ensure financial data survival.

---

## 1. Perpetual Tournament Engine
The system operates on an autonomous monthly cycle.
*   **Auto-Rotation:** At **00:00 UTC on the 1st of every month**, the system finalizes the current season (e.g., Season 1) and instantly initializes the next (Season 2).
*   **Idempotent Payouts:** Uses a unique `referenceId` for each winner (e.g., `TOURN_S1_RANK1`). If a server crashes during payout and restarts, the system attempts to pay again, but the **Vault rejects duplicate IDs**, ensuring zero double-spending.
*   **Rollover Logic:** If a season ends with zero participants, the prize pool automatically rolls over to the next season to maintain user interest.

## 2. Infrastructure Resilience (WAL)
Critical financial logging is prioritized for maximum safety.
*   **Write-Ahead Log (WAL):** High-priority events like `JACKPOT_WIN`, `WITHDRAWAL`, and `DEPOSIT` are written **synchronously** to the SQL ledger.
*   **Disaster Recovery:** In the event of a total server blackout, the WAL ensures that no pending jackpot win is lost, even if the application buffer was not yet flushed.

## 3. Security Sentinel (`SentinelWorker`)
*   **Ledger Reconciliation:** Every 10 minutes, this background process verifies the equation: `SUM(All Transactions) == Current User Balance`. Any discrepancy triggers an account freeze and admin alert.
*   **Chain Validation:** Scans the audit ledger for broken hash links, detecting any unauthorized manual database modifications.

## 4. Distributed Clustered Operations
*   **Redis Redlock:** Ensures that critical operations (e.g., claiming a jackpot) are mutually exclusive across multiple server nodes.
*   **Graceful Degression:** If the Redis cluster is unreachable, the automation layer falls back to local memory and persistent SQL flags to maintain availability.

## 5. Automated Promotional Layer
*   **`RaffleWorker`:** Randomly awards "Lucky Drops" to users who have been active within the last 3 minutes.
*   **`QuestService`:** Continuously tracks "Spin Count" and "Win Amount" goals, automatically crediting rewards via SignalR the moment a threshold is crossed.
