# 🏗️ Core Architecture: The AI-Driven Casino

AleaSim is built on a modular, enterprise-grade architecture that prioritizes security, mathematical integrity, and real-time player engagement.

---

## 1. The Logic Layers (The Trinity)

### Layer 1: The Game Director ("The Brain")
*   **Domain:** `AleaSim.Domain.Services.BrainService`
*   **Role:** Analyzes player history, current session "mood" (spin speed), and financial pool health.
*   **Goal:** Maximize player retention while enforcing the target house edge.

### Layer 2: The Game Engine ("The Executor")
*   **Domain:** `SlotGameEngine`, `BlackjackGameEngine`, etc.
*   **Role:** Applies game rules and executes spins.
*   **Integrity:** Uses **HMAC-SHA256** for Provably Fair results. No outcome is generated until both Server and Client seeds are combined.

### Layer 3: The Financial Vault ("The Guard")
*   **Domain:** `AleaSim.Domain.Services.VaultService`
*   **Role:** The absolute authority on money. 
*   **Control:** Manages the `GlobalPool` and user `ShadowWallets`. No win is paid unless the system is solvent.

---

## 2. Infrastructure & Resilience

### Real-Time Synchronization
*   **SignalR + Redis Backplane:** Ensures that 10,000+ players receive instant balance and jackpot updates across multiple server nodes.

### Fail-Safe Mechanism
*   **Redis Fallback:** Critical services (Cache, Locks) are "Dual-Track". If the Redis cluster fails, the platform automatically drops down to local memory to prevent downtime.

### Audit Immutable Ledger
*   **Chain of Trust:** Every significant event (Bet, Win, Admin Action) is hashed and linked to the previous event hash. This creates an unalterable audit trail verifyable by the built-in integrity tool.

---

## 3. Data Flow (Request Cycle)
1.  **User Spin:** Client sends `PlaceBet` request.
2.  **Vault:** Deducts funds + checks shadow balance.
3.  **Brain:** Decides if player needs a "Sugar Hit" (win) or "Cool Down".
4.  **Engine:** Generates HMAC outcome matching Brain's directive.
5.  **Vault:** Credits win + updates user pRTP stats.
6.  **Real-Time:** Pushes update to all connected UIs.
