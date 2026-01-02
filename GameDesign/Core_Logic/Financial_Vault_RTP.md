# 🏦 The Vault: RTP & Financial Control

## Philosophy
The Vault is the final gatekeeper. While The Brain tries to optimize Fun, The Vault ensures **Solvency**. It manages the flow of money and enforces the mathematical edge of the casino via "Shadow Accounting".

---

## Core Concepts

### 1. pRTP (Personal Return to Player)
Unlike traditional casinos that track RTP on a machine level, The Vault tracks RTP per **User**.
*   `User_Total_Wagered`: $5,000
*   `User_Total_Paid`: $4,200
*   `Current_pRTP`: 84%
*   `Target_pRTP`: 95%

The Brain uses this delta to request corrections. The Vault simply records it.

### 2. The Pools (The Bank)

The Vault manages hierarchical money pools:

*   **User Pool (Shadow Wallet):**
    *   Every time a user bets $1.00, $0.95 goes into their personal "Shadow Wallet" (theoretical return).
    *   $0.05 goes to the House Profit (Revenue).
    *   Wins are paid OUT of the Shadow Wallet.
    *   *Scenario:* If Shadow Wallet is empty, The Vault REJECTS big wins, forcing The Brain to issue a smaller win or a loss.

*   **Global Reserve (Jackpot Pool):**
    *   A fraction (e.g., 1%) of every bet feeds this pool.
    *   Used to pay out massive wins that exceed a user's personal shadow wallet.

### 3. Transaction Flow & Solvency

When The Brain requests a `TargetWin: $500`:

1.  **Check User Shadow Wallet:** Does User X have > $500 accrued in their theoretical return pool?
    *   *Yes:* Approve Transaction. Deduct from Shadow Wallet.
    *   *No:* Check Global Reserve? (Only if designated as a Random Jackpot).
    *   *No:* **REJECT Transaction.**

2.  **On Rejection:**
    *   The Vault returns `status: InsufficientFunds`.
    *   The Brain must downgrade the request (e.g., ask for $50 instead) or switch to a "Near Miss" (Visual excitement, zero cost).

---

## Security & Integrity
*   **Atomic Transactions:** Bet Deduction + Pool Update + Win Credit happen in a single database transaction.
*   **Audit Hash:** Every financial movement is hashed and appended to the ledger (as described in System Definition).
*   **Idempotency:** Unique Request IDs ensure no double-spending if the Brain retries a logic loop.
