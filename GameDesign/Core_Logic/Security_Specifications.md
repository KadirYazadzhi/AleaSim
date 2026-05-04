# 🛡️ Security & Infrastructure Specifications (v2.0)

AleaSim is designed with a "Bulletproof" architecture, implementing industrial-strength protections against common gambling platform attack vectors.

---

## 1. Session & API Security

### HttpOnly Cookie Shield
The `RefreshToken` is strictly stored in an **HttpOnly, Secure, SameSite=Strict** cookie. This prevents XSS attacks from stealing session identifiers, making token theft via JavaScript impossible.

### API Rate Limiting (DDoS & Bot Shield)
Integrated using the official ASP.NET Core `RateLimiter`:
*   **Global Limit:** 100 requests per 10 seconds per IP (prevents volumetric DDoS).
*   **Financial Limit:** 10 requests per 5 seconds per User (prevents bot-based balance draining or "button spamming").

---

## 2. Transactional Integrity

### Distributed Multi-Node Locking
Uses the **Redlock** algorithm via Redis to manage global state. 
*   **Wallet Guard:** Every bet or win operation locks the key `wallet_{userId}` for 5-10 seconds. This prevents "Double Spending" where a user attempts to place multiple parallel bets across different browser tabs or server nodes.
*   **Idempotent Settlements:** Every transaction uses a `referenceId` (UUID). The `VaultService` checks this ID against the ledger before processing, ensuring no transaction is ever applied twice.

### Atomic SQL Ledger
Balance updates and statistical increments never use "Read-Modify-Write." Instead, they use raw SQL atomic increments (`UPDATE ... SET Balance = Balance + @win`) to guarantee 100% precision even under extreme load.

---

## 3. Cryptographic Trust (Hybrid RNG)

AleaSim uses a dual-engine RNG system to ensure Provable Fairness and regulatory compliance:

| Mode | Algorithm | Context |
| :--- | :--- | :--- |
| **Provably Fair** | HMAC-SHA256 | Used for standard spins where ServerSeed + ClientSeed are provided. |
| **Certified Crypto** | RandomNumberGenerator | Fallback used when seeds are missing to provide hardware-backed entropy. |

---

## 4. Infrastructure Resilience (WAL)

The system implement **Write-Ahead Logging (WAL)** for high-priority financial data.
*   **WAL Bypass:** Critical events like `JACKPOT_WIN`, `WITHDRAWAL`, and `DEPOSIT` bypass the internal background buffer.
*   **Synchronous Persistence:** These events are written immediately and synchronously to the SQL database. 
*   **Safe Recovery:** In case of a sudden power loss or server crash, the system can fully reconstruct the last state of any critical win, ensuring no player loses their prize.
