# 🛡️ Security & Infrastructure Specifications

AleaSim is designed with a "Security First" mindset, implementing advanced protections against common gambling platform attack vectors.

---

## 1. Authentication & Session Security

### HttpOnly Cookie Architecture
*   **Protection:** The `RefreshToken` is stored in a strictly **HttpOnly, Secure, SameSite=Strict** cookie.
*   **Defense:** This prevents all JavaScript-based (XSS) attempts to steal session tokens.

### Token Zombie Prevention
*   **Logic:** Every API request validates the token's `jti` (unique ID) against the `UserSession` table.
*   **Real-time Revocation:** If a session is terminated by an admin or the user logs out, all associated access tokens become invalid within 2 minutes (cached) or immediately (DB).

---

## 2. Transaction Integrity

### Atomic Operations
*   **No Race Conditions:** Balance deductions and win credits are performed using raw SQL atomic increments.
*   **Distributed Locking:** Every wallet operation is wrapped in a Redis-based lock (`wallet_{userId}`). This prevents "Double Spending" where a user attempts multiple parallel bets.

### Faucet Security
*   **Throttling:** Max 1 claim per hour.
*   **Multi-layer Check:** Combined Redis rate-limit + Database history check + Distributed locking.

---

## 3. Cryptographic Trust (Provably Fair)

### The HMAC Chain
1.  **Commitment:** Server generates a `ServerSeed` and shows the user its `SHA256 Hash`.
2.  **Entropy:** User provides a `ClientSeed`.
3.  **Reveal:** Once the session ends, the `ServerSeed` is revealed.
4.  **Proof:** Result = `First8Bytes(HMAC-SHA256(ServerSeed, ClientSeed + Nonce))`.

---

## 4. Platform Hardening

### DoS Protection
*   **Payload Limit:** 1MB maximum JSON request size.
*   **Rate Limiting:** IP-based throttling for Login (10/min), Register (5/hour), and Support (10/hr).

### Database Resilience
*   **Auto-Migrations:** The system automatically applies SQL migrations upon startup, ensuring the schema is always up-to-date.
*   **Snapshotting:** Background services perform daily cleanup of old statistics to prevent database bloat.
