# ⚡ Optimization & Data Strategy

AleaSim is optimized for high-throughput gaming sessions through a tiered state management approach.

---

## 1. Hot vs. Cold Storage

### Hot State (Redis / Local Cache)
*   **Purpose:** Instant access to active game rounds.
*   **Stored Data:** 
    *   Active Slot Grids (Respin/Bonus state).
    *   Blackjack Hands.
    *   User Online Status.
    *   Recent Audit Log Hashes.
*   **Resilience:** Every engine implements a **Graceful Fallback**. If Redis is unreachable, the system transparently utilizes local `IMemoryCache`.

### Cold Storage (SQL)
*   **Purpose:** Permanent record of platform activity.
*   **Stored Data:**
    *   Completed Game Rounds.
    *   Transaction Ledger.
    *   User Account Data.
*   **Write Strategy:** Critical financial data is written immediately. Non-critical audit logs use an **Asynchronous Buffer** to batch writes every 5 seconds.

---

## 2. State Restoration (UX Recovery)
*   **The Problem:** Browser refreshes or disconnects during a bonus game can lead to player frustration.
*   **The Solution:** 
    1.  Engines persist partial states (e.g., Slot Lives = 2) to Redis/DB on every spin.
    2.  Blazor frontend calls `ResumeSession` on load.
    3.  UI visually reconstructs the previous state (Grid, Scores, Cards) with animation.

---

## 3. High-Performance Math
*   **Pre-computed Strips:** Game engines use static reel strips to minimize CPU usage.
*   **Batch Payouts:** Tournament payouts use a single SQL transaction to process Top 10 winners simultaneously, minimizing DB lock contention.
