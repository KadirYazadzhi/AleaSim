# 🏦 The Vault: Financial Integrity & RTP Control

The Vault is the primary financial controller of AleaSim. It manages all monetary movements, enforces the casino's mathematical edge, and ensures the platform remains solvent through shadow accounting.

---

## 1. pRTP (Personal Return to Player)
AleaSim implements **User-Level RTP Tracking**.
*   **accruedPool:** A per-user "theoretical return" balance.
*   **House Edge:** A fixed percentage (e.g., 4%) is deducted from every bet and sent to "House Revenue".
*   **Win Authorization:** Big wins are checked against the user's `accruedPool` and the `GlobalPool`. If both are insufficient, the win is denied, forcing the Brain to generate a smaller win or a Near Miss.

---

## 2. Advanced Wallet Protection

### Atomic SQL Updates
To prevent Race Conditions (Double Spending) in a high-concurrency environment, AleaSim never uses "Read -> Modify -> Write".
*   **Mechanism:** Direct SQL atomic increments: `UPDATE RTPStatistics SET TotalWagered = TotalWagered + @bet WHERE Id = @id`.
*   **Benefit:** 100% accurate financial stats regardless of parallel request volume.

### Distributed Locking
*   **Scope:** All balance-altering operations (PlaceBet, ClaimWin, Faucet, Jackpot).
*   **Key:** `wallet_{userId}` or `jackpot_claim_{tier}`.
*   **Mechanism:** Redis-based semaphores with a 5-second timeout.

---

## 3. Responsible Gaming Enforcement

### Daily Loss Limit
*   **Validation:** Intercepts every `PlaceRound` request.
*   **Calculation:** `SUM(Today_Bets) - SUM(Today_Wins)`.
*   **Blocking:** If `Net_Loss + Current_Bet > Daily_Limit`, the bet is rejected with a descriptive error.

### Faucet Security (Relief)
*   **Eligibility:** Balance < $10.00.
*   **Limit:** Max 1 claim per hour.
*   **Protection:** Distributed locking + Redis rate limiting + Transaction history check to prevent parallel claim exploits.

---

## 4. Transaction Immutable Ledger
*   **Persistence:** Every financial movement generates a `Transaction` record.
*   **Resulting Balance:** Every record stores the final balance *after* the operation, enabling instant historical reconciliation by the Sentinel worker.
