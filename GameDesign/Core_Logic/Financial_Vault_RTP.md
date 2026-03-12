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

---

## 5. Shadow Balance Formula

The **Shadow Balance** represents the Vault's theoretical remaining payout liability to a specific player. It answers: *"How much more should this player statistically win before they reach their target RTP?"*

```
ShadowBalance = SUM(AllBets) × TargetRTP − SUM(AllWins)
```

| Variable | Source | Description |
| :--- | :--- | :--- |
| `SUM(AllBets)` | `RTPStatistics.TotalWagered` | Lifetime total amount wagered by this player |
| `TargetRTP` | Platform config (e.g., 0.95) | The configured return-to-player percentage |
| `SUM(AllWins)` | `RTPStatistics.TotalWon` | Lifetime total amount won by this player |

**Interpretation:**
- **Positive ShadowBalance** → Player is below their expected return; the Vault "owes" them statistically. Brain may bias toward wins.
- **Zero ShadowBalance** → Player is exactly at target RTP. Normal RNG.
- **Negative ShadowBalance** → Player has won more than their statistical share. Vault suppresses wins to reclaim balance.

**Example:**
```
TotalWagered = $1,000
TargetRTP    = 95%  → Expected total wins = $950
TotalWon     = $880

ShadowBalance = $1,000 × 0.95 − $880 = $950 − $880 = +$70
(Player is $70 below expected return — slight positive bias applied)
```

The `ShadowBalance` is not stored as a column; it is computed on-the-fly from `TotalWagered` and `TotalWon` to prevent any stored value from drifting out of sync with the ledger.

---

## 6. Full pRTP Tracking Lifecycle

The following steps describe the complete lifecycle of a single round from the Vault's perspective.

```
Step 1 — Bet Placement
  Player submits a $10 spin request.
  Vault validates:
    ├── User balance ≥ $10 (distributed lock acquired: wallet_{userId})
    ├── Daily loss limit not exceeded
    └── IsFeatureActive bet-lock check (if in bonus feature, bet must match locked amount)
  Vault atomically deducts $10:
    UPDATE Wallets SET Balance = Balance - 10 WHERE UserId = @id
  Vault atomically records the wagered amount:
    UPDATE RTPStatistics SET TotalWagered = TotalWagered + 10 WHERE UserId = @id
  Transaction record inserted: Type=Bet, Amount=-10, ResultingBalance=<new balance>

Step 2 — Brain Consultation
  Brain reads RTPStatistics (TotalWagered, TotalWon) for this user.
  Computes ShadowBalance and pRTP delta.
  Reads ChurnRisk score.
  Writes DecisionType directive to Redis: brain:directive:{userId}

Step 3 — RNG Outcome Generation
  Game engine reads Brain directive.
  Generates spin outcome via HMAC-SHA256(ServerSeed + ClientSeed + Nonce).
  If directive requires re-roll, nonce is incremented and a new hash is generated.
  Final outcome is selected (compliant with directive + mathematically valid).

Step 4 — Win Authorization
  Calculated win amount = $25 (example).
  Vault checks:
    ├── accruedPool (user-level): Is ShadowBalance ≥ $25?
    └── GlobalPool: Is the platform-wide pool solvent?
  If both checks pass → Win AUTHORIZED.
  If either check fails → Win DENIED; Brain instructed to downgrade to Near Miss.

Step 5 — Win Settlement (if authorized)
  Vault atomically credits $25:
    UPDATE Wallets SET Balance = Balance + 25 WHERE UserId = @id
  Vault atomically records the win:
    UPDATE RTPStatistics SET TotalWon = TotalWon + 25 WHERE UserId = @id
  Transaction record inserted: Type=Win, Amount=+25, ResultingBalance=<new balance>
  Distributed lock released: wallet_{userId}

Step 6 — House Edge Accrual
  House edge portion ($10 × 4% = $0.40) is credited to HouseRevenue ledger:
    UPDATE HouseRevenue SET TotalAccrued = TotalAccrued + 0.40
  This happens atomically alongside Step 1.

Step 7 — Sentinel Reconciliation (async)
  Background Sentinel worker periodically verifies:
    SUM(Transaction.Amount WHERE UserId = X) == CurrentBalance(X)
  Any discrepancy triggers an alert and automatic rollback investigation.
```

---

## 7. Bonus Wagering Progress Calculation

When a player claims a bonus (e.g., a 100% deposit match), the bonus funds are subject to a **wagering requirement** before they can be withdrawn as real money.

### Wagering Requirement Tracking
```
WageringProgress = SUM(BonusBets) / (BonusAmount × WageringMultiplier)
```

| Variable | Description |
| :--- | :--- |
| `BonusBets` | Total amount wagered using bonus balance (tracked separately from real-money bets) |
| `BonusAmount` | Original bonus grant amount |
| `WageringMultiplier` | Platform-configured multiplier (e.g., 30x means wagering $3,000 to clear a $100 bonus) |

**Example:**
```
BonusAmount         = $100
WageringMultiplier  = 30x → Required wagering = $3,000
BonusBets so far    = $1,200
WageringProgress    = $1,200 / $3,000 = 40%
```

### Conversion to Real Money
When `WageringProgress ≥ 1.0 (100%)`:
1. The bonus balance is converted to real money: `Wallets.BonusBalance → Wallets.RealBalance`.
2. A `Transaction` record is inserted: `Type = BonusConversion, Amount = +<bonus_amount>`.
3. The `PlayerBonus` record is marked `Status = Completed`.
4. The player can now withdraw the converted amount.

### Expiry
If the wagering deadline is exceeded (`PlayerBonus.ExpiresAt < NOW()`):
1. The remaining `BonusBalance` is forfeited.
2. A `Transaction` record is inserted: `Type = BonusExpiry, Amount = -<remaining_balance>`.
3. The `PlayerBonus` record is marked `Status = Expired`.

---

## 8. Step-by-Step Transaction Trace Example

**Scenario:** Player places a $10.00 bet and wins $25.00.

### Database Records Created/Modified

#### Wallets table (atomic UPDATE)
```sql
-- Step 1: Deduct bet
UPDATE Wallets
SET Balance = Balance - 10.00,
    UpdatedAt = NOW()
WHERE UserId = 'alice-uuid';
-- Resulting Balance: $990.00 (was $1,000.00)

-- Step 5: Credit win
UPDATE Wallets
SET Balance = Balance + 25.00,
    UpdatedAt = NOW()
WHERE UserId = 'alice-uuid';
-- Resulting Balance: $1,015.00
```

#### RTPStatistics table (atomic UPDATE)
```sql
-- Step 1: Record wagered amount
UPDATE RTPStatistics
SET TotalWagered = TotalWagered + 10.00,
    UpdatedAt = NOW()
WHERE UserId = 'alice-uuid';

-- Step 5: Record win amount
UPDATE RTPStatistics
SET TotalWon = TotalWon + 25.00,
    UpdatedAt = NOW()
WHERE UserId = 'alice-uuid';
```

#### Transactions table (INSERT records)
```sql
-- Bet record
INSERT INTO Transactions (Id, UserId, Type, Amount, ResultingBalance, RoundId, CreatedAt)
VALUES ('txn-001', 'alice-uuid', 'Bet', -10.00, 990.00, 'round-xyz', NOW());

-- Win record
INSERT INTO Transactions (Id, UserId, Type, Amount, ResultingBalance, RoundId, CreatedAt)
VALUES ('txn-002', 'alice-uuid', 'Win', +25.00, 1015.00, 'round-xyz', NOW());
```

### Redis Keys Modified

| Key | Operation | Value After |
| :--- | :--- | :--- |
| `wallet_alice-uuid` (distributed lock) | SET with 5s TTL | `"locked"` (during transaction) |
| `brain:directive:alice-uuid` | GET + DEL (consumed) | *(deleted after use)* |
| `session:alice-uuid:lastActivity` | SET | `<current unix timestamp>` |
| `ratelimit:bet:alice-uuid` | INCR | Counter incremented (rate limit check) |

### Summary of the $10 Bet → $25 Win Round
| Event | Balance Before | Amount | Balance After |
| :--- | :---: | :---: | :---: |
| Bet placed | $1,000.00 | −$10.00 | $990.00 |
| Win credited | $990.00 | +$25.00 | $1,015.00 |
| House edge accrued | (house ledger) | +$0.40 | (internal) |
| **Net player P&L** | | **+$15.00** | |
| **Net house P&L** | | **−$14.60** *(loss on this round)* | |
