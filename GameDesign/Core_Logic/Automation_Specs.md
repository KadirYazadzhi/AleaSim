# 🤖 Automation & System Reliability

AleaSim relies on a series of specialized background workers to maintain platform integrity, process promotions, and optimize data storage.

---

## 1. The Tournament Engine (`TournamentPayoutWorker`)
*   **Cycle:** Monthly. Checks for finalization every hour.
*   **Finalization Date:** Strictly on the **1st of every month**.
*   **Ranking:** ROI-based (`((Wins-Bets)/Bets) * 100`) calculated across the entire month's wagering volume.
*   **Prize Pool:** Base $25,000 + 1% of total platform wagering volume for that month.
*   **Idempotency:** Uses a database-backed execution flag (`TournamentPaid_YYYY_MM`) within a SQL transaction to guarantee Top 10 winners are paid exactly once.

## 2. Security & Compliance (`SentinelWorker`)
*   **Financial Reconciliation:** Every 10 minutes, the worker verifies that `Sum(Transactions) == CurrentBalance` for all users. Discrepancies are logged as "Critical Anomaly" alerts.
*   **Old Data Cleanup:** 
    *   **RTP Statistics:** Records older than 30 days are purged to keep performance high.
    *   **Audit Logs:** Logs older than 90 days are archived/deleted.
*   **Presence Tracking:** Periodically sweeps Redis to ensure "Online" counts reflect real connections.

## 3. Distributed Infrastructure
*   **Redis Locks:** All critical operations (Betting, Claiming Jackpots, Faucet) use distributed locks to prevent "Double Spend" or concurrent request abuse in a clustered environment.
*   **Graceful Fallback:** If the Redis cluster is unreachable, all services automatically fall back to local `IMemoryCache` and `InMemoryLocks` to maintain uptime.

## 4. Financial Workers
*   **`RaffleWorker`:** Randomly distributes prize drops to **ACTIVE** players (must have bet in the last 3 minutes).
*   **`DailyBonusWorker`:** Resets the "Daily Spin" eligibility at 00:00 UTC and calculates daily retention cashback stats.
*   **`AuditWriterWorker`:** An asynchronous batch-writer that flushes the `IAuditBuffer` queue to the database every 5 seconds or 100 logs, optimizing disk throughput.
