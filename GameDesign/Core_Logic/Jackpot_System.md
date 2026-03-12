# 💰 Jackpot System — AleaSim Design Document

## 📋 Overview

The AleaSim Jackpot System provides four progressive jackpot tiers that grow with every bet placed across all participating games. Wins are broadcast in real time to all connected clients via SignalR, creating a shared excitement loop. The system is designed for correctness under high concurrency using distributed locks and Redis atomic operations, with full auditability through a historical jackpot ledger.

---

## 🏆 Jackpot Tiers

| Tier      | Emoji | Seed Value  | Typical Win Range   | Max Cap     | Contribution Rate |
|-----------|-------|-------------|---------------------|-------------|-------------------|
| **Mini**  | 🥉    | $10.00      | $10 – $100          | $250        | 0.10% of bet      |
| **Minor** | 🥈    | $100.00     | $100 – $1,000       | $5,000      | 0.15% of bet      |
| **Major** | 🥇    | $1,000.00   | $1,000 – $25,000    | $50,000     | 0.20% of bet      |
| **Mega**  | 💎    | $10,000.00  | $10,000 – $1,000,000 | Uncapped   | 0.05% of bet      |

> **Seed Value**: The pool resets to this value immediately after a payout.  
> **Max Cap**: If the pool exceeds this value, contributions pause until a win occurs (Mini/Minor/Major only). Mega is uncapped to enable life-changing prizes.

### Total Contribution Per Bet

For a $10.00 bet, total jackpot contribution:

```
Mini:  $10.00 × 0.0010 = $0.010
Minor: $10.00 × 0.0015 = $0.015
Major: $10.00 × 0.0020 = $0.020
Mega:  $10.00 × 0.0005 = $0.005
─────────────────────────────────
Total: $10.00 × 0.0050 = $0.050  (0.5% total per bet)
```

---

## 📈 Pool Contribution Flow

```
Player places bet ($B)
        │
        ▼
BaseGameEngine resolves round
        │
        ▼
JackpotContributionHandler (INotificationHandler<SpinCompletedEvent>)
        │
        ├─► For each tier:
        │       contribution = B × rate
        │       if pool < maxCap:
        │           INCRBYFLOAT jackpot:pool:{tier} {contribution}
        │
        └─► Broadcast updated pool values via SignalR → all clients
                (throttled: max 1 broadcast/sec per tier)
```

### Redis Pool Keys

```
jackpot:pool:mini    → FLOAT (current Mini pool in USD)
jackpot:pool:minor   → FLOAT
jackpot:pool:major   → FLOAT
jackpot:pool:mega    → FLOAT
```

All increments use `INCRBYFLOAT`, which is atomic at the Redis command level.

---

## 🎲 Trigger Probability

Each spin has a per-tier evaluation after the game result is resolved. The `BrainService` can influence eligibility but **never guarantees** a jackpot win.

| Tier      | Base Probability (per spin) | Brain Influence                                   |
|-----------|-----------------------------|---------------------------------------------------|
| **Mini**  | 1 in 500                    | Brain can increase up to 1 in 100 for target user |
| **Minor** | 1 in 5,000                  | Brain can increase up to 1 in 500                 |
| **Major** | 1 in 50,000                 | Brain can increase up to 1 in 5,000               |
| **Mega**  | 1 in 1,000,000              | Brain influence: ±10% of base probability         |

### Evaluation Logic

```csharp
public async Task<JackpotTier?> EvaluateTriggerAsync(string userId, decimal betAmount)
{
    var brainModifiers = await _brainService.GetJackpotModifiersAsync(userId);

    // Evaluate from highest to lowest tier (only one tier wins per spin)
    foreach (var tier in new[] { JackpotTier.Mega, JackpotTier.Major, JackpotTier.Minor, JackpotTier.Mini })
    {
        var baseProbability = _config[tier].BaseProbability;
        var modifier        = brainModifiers.GetValueOrDefault(tier, 1.0);
        var effectiveProb   = baseProbability * modifier;

        if (_rng.NextDouble() < effectiveProb)
            return tier;
    }

    return null;
}
```

> **Minimum bet threshold**: Players must bet at least $0.10 to be eligible for jackpot evaluation. Bonus-only balance bets are excluded from Mega tier eligibility.

---

## 🔒 Claim Security

Jackpot claims are the most security-critical operation in the system. A race condition here could result in double-payouts worth millions of dollars. AleaSim uses a multi-layer approach.

### Distributed Lock

```
Key:    jackpot_claim_{tier}          (e.g., jackpot_claim_mega)
TTL:    30 seconds
Value:  {claimRequestId}              (UUID v4 generated per claim attempt)
```

Implemented via **Redlock** algorithm across 3 independent Redis nodes:

```csharp
public async Task<JackpotClaimResult> ClaimJackpotAsync(string userId, JackpotTier tier)
{
    var lockKey   = $"jackpot_claim_{tier.ToString().ToLower()}";
    var claimId   = Guid.NewGuid().ToString();

    await using var redLock = await _redLockFactory.CreateLockAsync(
        lockKey,
        expiryTime:  TimeSpan.FromSeconds(30),
        waitTime:    TimeSpan.FromSeconds(5),
        retryTime:   TimeSpan.FromMilliseconds(500));

    if (!redLock.IsAcquired)
        return JackpotClaimResult.AlreadyClaimed;

    // Atomic read-and-reset in a Lua script
    var poolValue = await _redis.ScriptEvaluateAsync(ClaimLuaScript, new RedisKey[]
    {
        $"jackpot:pool:{tier.ToString().ToLower()}"
    }, new RedisValue[]
    {
        _config[tier].SeedValue.ToString()
    });

    if ((decimal)poolValue <= 0)
        return JackpotClaimResult.PoolEmpty;

    // Persist to SQL ledger before releasing lock
    await _jackpotLedger.RecordWinAsync(userId, tier, (decimal)poolValue, claimId);

    // Credit player via VaultService
    await _vaultService.CreditJackpotWinAsync(userId, (decimal)poolValue, claimId);

    return new JackpotClaimResult(Success: true, Amount: (decimal)poolValue, ClaimId: claimId);
}
```

### Claim Lua Script (Atomic Reset)

```lua
-- KEYS[1] = pool key, ARGV[1] = seed value
local current = tonumber(redis.call('GET', KEYS[1]))
if current == nil or current <= 0 then
    return 0
end
redis.call('SET', KEYS[1], ARGV[1])
return current
```

This ensures the pool value is read **and** reset atomically — no other process can read the old value after the reset.

### Double-Claim Prevention

| Layer | Mechanism                                               |
|-------|---------------------------------------------------------|
| L1    | Redlock distributed lock (30s TTL)                     |
| L2    | Lua atomic read-reset (Redis single-threaded)          |
| L3    | Idempotency key (`claimId`) in SQL `JackpotWins` table  |
| L4    | VaultService deduplication check on `claimId`           |

---

## 📡 Real-Time Broadcast

On a successful jackpot win, a SignalR message is pushed to **all connected clients** via `WinnersHub`:

```csharp
await _hubContext.Clients.All.SendAsync("JackpotWon", new JackpotWinBroadcast
{
    Tier        = tier.ToString(),    // "Mega"
    Amount      = wonAmount,          // 842631.50
    WinnerAlias = maskedAlias,        // "J***n" (masked for privacy)
    GameName    = gameName,           // "Starburst XXXtreme"
    WonAt       = DateTime.UtcNow,
    NewPoolValue = seedValue          // reset value
});
```

### Client Rendering

On the frontend, `JackpotWon` events trigger:
1. Full-screen animated overlay with tier-specific particle effect.
2. Jackpot ticker widget updates to the new (seed) pool value.
3. Toast notification: *"💎 MEGA JACKPOT WON! J***n just won $842,631.50 on Starburst!"*
4. Sound effect (tier-specific fanfare, toggleable in settings).

### Pool Value Broadcasts (Ticker Updates)

The current pool values are broadcast to all clients every **1 second** via a `BackgroundService`:

```csharp
// Throttled ticker update — one broadcast per second max
HGETALL jackpot:pools → broadcast via GameHub "JackpotPoolUpdate"
```

---

## 🔄 Reset After Payout

Immediately after a successful claim (within the same Lua script):

1. Pool is reset to **seed value**.
2. A `JackpotReset` event is published internally (triggers contribution resumption if pool was capped).
3. Broadcast sent to all clients with the new (reset) pool value.

```
Timeline:
  T+0ms   Jackpot triggered
  T+10ms  Distributed lock acquired
  T+12ms  Lua script: read pool, set to seed
  T+15ms  SQL ledger record inserted
  T+20ms  VaultService credit executed
  T+25ms  SignalR JackpotWon broadcast
  T+26ms  Lock released
```

---

## 📚 Historical Jackpot Ledger

All jackpot wins are persisted in SQL for compliance, audit, and display purposes.

```sql
CREATE TABLE JackpotWins (
    Id            BIGINT PRIMARY KEY AUTO_INCREMENT,
    ClaimId       VARCHAR(64)     NOT NULL UNIQUE,   -- idempotency key
    UserId        VARCHAR(64)     NOT NULL,
    Tier          VARCHAR(16)     NOT NULL,           -- Mini/Minor/Major/Mega
    Amount        DECIMAL(18, 2)  NOT NULL,
    GameId        VARCHAR(64)     NOT NULL,
    SessionId     VARCHAR(64)     NOT NULL,
    WonAt         DATETIME        NOT NULL,
    SeedResetTo   DECIMAL(18, 2)  NOT NULL,
    Paid          BOOL            NOT NULL DEFAULT FALSE,
    INDEX idx_user     (UserId),
    INDEX idx_tier     (Tier),
    INDEX idx_won_at   (WonAt)
);

CREATE TABLE JackpotPoolHistory (
    Id          BIGINT PRIMARY KEY AUTO_INCREMENT,
    Tier        VARCHAR(16)     NOT NULL,
    PoolValue   DECIMAL(18, 2)  NOT NULL,
    RecordedAt  DATETIME        NOT NULL,
    INDEX idx_tier_time (Tier, RecordedAt)
);
```

`JackpotPoolHistory` is populated by a scheduled job every **15 minutes**, enabling historical pool growth charts in the Admin Panel.

---

## 🏦 VaultService Integration

`VaultService` is the single authoritative service for all financial credits and debits.

### Jackpot Credit Flow

```csharp
public async Task CreditJackpotWinAsync(string userId, decimal amount, string claimId)
{
    // Idempotency check
    if (await _db.JackpotWins.AnyAsync(w => w.ClaimId == claimId && w.Paid))
        return; // already credited, safe to ignore

    await using var tx = await _db.Database.BeginTransactionAsync();

    await _db.WalletTransactions.AddAsync(new WalletTransaction
    {
        UserId      = userId,
        Amount      = amount,
        Type        = TransactionType.JackpotWin,
        Reference   = claimId,
        CreatedAt   = DateTime.UtcNow
    });

    await _db.JackpotWins
        .Where(w => w.ClaimId == claimId)
        .ExecuteUpdateAsync(s => s.SetProperty(w => w.Paid, true));

    await tx.CommitAsync();

    // Notify compliance system for wins above reporting threshold ($10,000)
    if (amount >= 10_000)
        await _complianceService.ReportLargeWinAsync(userId, amount, claimId);
}
```

---

## 📊 Admin Panel Visibility

Admins have read access to:

| Widget                          | Data Source                          |
|---------------------------------|--------------------------------------|
| Live pool values (all 4 tiers)  | Redis `jackpot:pool:*`               |
| Recent wins table               | SQL `JackpotWins` (last 50)          |
| Pool growth chart (7 days)      | SQL `JackpotPoolHistory`             |
| Total paid out (all time)       | Aggregated from `JackpotWins`        |
| Contribution rate config        | In-memory config (hot-reload)        |

Admins can also **manually seed** a tier pool (e.g., for promotional events) with a full audit trail entry.
