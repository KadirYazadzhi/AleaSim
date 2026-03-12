# 🎰 AleaSim: The Trinity Casino Architecture

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WASM-5C2D91?style=flat-square&logo=blazor)
![Redis](https://img.shields.io/badge/Redis-Active-DC382D?style=flat-square&logo=redis)
![Security](https://img.shields.io/badge/Security-Production--Ready-success?style=flat-square)
![Provably Fair](https://img.shields.io/badge/RNG-HMAC--SHA256-orange?style=flat-square)
![k3s](https://img.shields.io/badge/Hosted%20on-k3s%20Cluster-326CE5?style=flat-square&logo=kubernetes)
![SignalR](https://img.shields.io/badge/Real--Time-SignalR-512BD4?style=flat-square)
![MySQL](https://img.shields.io/badge/Database-MySQL%2FMariaDB-4479A1?style=flat-square&logo=mysql)
![EF Core](https://img.shields.io/badge/ORM-EF%20Core%208-512BD4?style=flat-square)
![xUnit](https://img.shields.io/badge/Tests-xUnit-green?style=flat-square)

**AleaSim** is a high-performance, enterprise-grade gambling simulation platform built on the **Trinity Architecture** — a radical departure from standard RNG. Every outcome is **mathematically deterministic**, **cryptographically verifiable**, and **AI-optimized** to balance player retention against house edge. It merges classic gambling mechanics with deep RPG progression, real-time social features, and a microservice-oriented backend — deployed on a personal k3s Kubernetes cluster.

> [!IMPORTANT]
> This is a **simulation platform** built for research, entertainment software design study, and engineering demonstration. It is not connected to real money, real gambling licenses, or any live financial systems.

> **[SCREENSHOT: Main Platform Banner / Dashboard Hero]** *(Place image at `docs/images/banner.png`)*

---

## 📸 Gallery & UI

| | |
| :---: | :---: |
| **Dashboard / Home** | **CloverChase Slot Gameplay** |
| > **[SCREENSHOT: Dashboard]** *(Place image at `docs/images/dashboard.png`)* | > **[SCREENSHOT: CloverChase Gameplay]** *(Place image at `docs/images/cloverchase-gameplay.png`)* |
| **Blackjack / Table Games** | **RPG Progression & Quest Board** |
| > **[SCREENSHOT: Blackjack Table]** *(Place image at `docs/images/blackjack.png`)* | > **[SCREENSHOT: RPG & Quest Board]** *(Place image at `docs/images/rpg-quests.png`)* |
| **Admin Panel & Live Controls** | **Live Winners Feed & Chat** |
| > **[SCREENSHOT: Admin Panel]** *(Place image at `docs/images/admin-panel.png`)* | > **[SCREENSHOT: Live Winners & Chat]** *(Place image at `docs/images/live-feed.png`)* |
| **Bell Bonus Game** | **Mobile View / Responsive Layout** |
| > **[SCREENSHOT: Bell Bonus]** *(Place image at `docs/images/bell-bonus.png`)* | > **[SCREENSHOT: Mobile Responsive View]** *(Place image at `docs/images/mobile-view.png`)* |

---

## 🌐 Live Deployment — Personal k3s Cluster

> [!NOTE]
> AleaSim is deployed on a self-hosted **k3s** (lightweight Kubernetes) cluster rather than a managed cloud provider — demonstrating full-stack DevOps ownership from code to production.

### What is k3s and Why?

[k3s](https://k3s.io/) is a certified, production-grade Kubernetes distribution optimized for resource-constrained environments. It packages the full Kubernetes control plane into a single ~70MB binary. It was chosen because it allows running a genuine multi-service Kubernetes environment on commodity hardware with low overhead, while retaining full Kubernetes API compatibility (Helm charts, `kubectl`, Ingress, etc.).

### Cluster Architecture

The cluster runs multiple coordinated pods across the following services:

| Pod / Service | Role |
| :--- | :--- |
| `aleasim-api` | ASP.NET Core 8 API + SignalR Hub + Background Workers |
| `aleasim-client` | Blazor WASM static files served via Nginx |
| `redis` | SignalR backplane, distributed locks, session cache |
| `mysql` | Persistent relational store (players, transactions, audit logs) |

- **Internal Communication:** All services communicate via **ClusterIP** services — zero external exposure between pods.
- **Ingress:** An Nginx Ingress Controller (or NodePort where appropriate) routes external HTTPS traffic to the correct service, with TLS termination.
- **Kubernetes Benefits in Practice:**
  - **Auto-restart:** If the API pod crashes, Kubernetes restarts it automatically via its `RestartPolicy`.
  - **Rolling Updates:** Zero-downtime deploys via `RollingUpdate` deployment strategy.
  - **Resource Limits:** CPU and memory requests/limits are defined per pod to prevent runaway processes from starving neighbours.
  - **ConfigMaps / Secrets:** All connection strings and JWT keys are injected as Kubernetes Secrets — never baked into the image.

> **[SCREENSHOT: k3s Cluster Diagram]** *(Place image at `docs/images/k3s-cluster-diagram.png`)*

> [!TIP]
> See [docs/k3s/deployment-guide.md](docs/k3s/deployment-guide.md) for the full Kubernetes manifest walkthrough and `kubectl` command reference.

---

## ⚙️ The Request Lifecycle: What Happens When You Click "Spin"?

The platform does not simply generate a random number. A single HTTP POST to `GameController.cs` triggers a deeply orchestrated pipeline managed by `GameDirector.cs`. Below is the complete flow, including every error path.

### Visual Flow

```
[Client Click "Spin"]
        │
        ▼
[GameController.cs]  ──── HTTP 429 if rate-limited ──────────────────────────┐
        │                                                                      │
        ▼                                                                      │
[GameDirector.cs]                                                             │
   ├─ 1. Session Hijack Check  ─── FAIL → HTTP 401 Unauthorized ─────────────┤
   ├─ 2. Anti-Bot Throttle (300ms min) ─── FAIL → HTTP 429 Too Many Requests ┤
   ├─ 3. Daily Loss Limit Check ─── FAIL → HTTP 403 Limit Reached ───────────┤
   │                                                                           │
   ▼                                                                           │
[VaultService.cs — AcquireLock("wallet_{userId}")]                            │
   ├─ Redis DOWN → Falls back to InMemoryLockService (single-node safe) ──────┤
   ├─ Balance insufficient → HTTP 400 Insufficient Funds ─────────────────────┤
   │                                                                           │
   ▼                                                                           │
[BrainService.cs — Analyse & Issue Directive]                                 │
   ├─ No suitable outcome found → BrainDirective = NearMiss ──────────────────┤
   │                                                                           │
   ▼                                                                           │
[GameEngine.cs — Execute Directive]                                           │
   ├─ Paytable lookup fails → fallback to safe zero-win outcome ──────────────┤
   │                                                                           │
   ▼                                                                           │
[VaultService.cs — Atomic Settlement]                                         │
   ├─ CanAffordWinCheck fails → win rejected, Brain reissues NearMiss ────────┤
   │                                                                           │
   ▼                                                                           │
[AuditBuffer → AuditWriterWorker]                                             │
        │                                                                      │
        ▼                                                                      │
[SignalR Hub → Client UI Update] ◄────────────────────── All errors ─────────┘
```

### Step-by-Step Breakdown

1. **Security & Rate Limiting (`GameDirector`):** Validates `sessionId` against `userId` (Session Hijack prevention). Checks `LastBetTimestamp` — spins faster than 300ms are rejected. Responsible Gaming enforced: `DailyLossLimit` checked before any stake is accepted.

2. **Financial Lock (`VaultService`):** Acquires a Distributed Lock in Redis (`wallet_{userId}`). If Redis is unavailable, the system falls back to `InMemoryLockService` — functional in single-node mode. Ten simultaneous bet requests from the same user? Nine of them queue; the first wins the lock; all others wait in line.

3. **Behavioral Analysis (`BrainService`):** The Brain queries the player's `BehaviourProfile` — loss streak, average spin interval, pRTP delta, and LTV tier — and issues a typed `BrainDirective`. If no suitable behavioral outcome is found (e.g., RTP budget is exhausted), the Brain defaults to a `NearMiss` directive.

4. **Outcome Construction (`GameEngine`):** The specific engine (`SlotGameEngine`, `BlackjackGameEngine`, etc.) receives the directive and performs **Reverse Engineering** — instead of spinning randomly, it consults the paytable and mathematically forces reel strips to produce the exact win value requested. If the paytable cannot satisfy the directive (edge case), the engine falls back to a safe zero-win outcome.

5. **Win Approval (`VaultService` — `CanAffordWinCheck`):** Before crediting, the Vault validates that the global `PoolBalance` can absorb the win AND that the user's `ShadowBalance` does not violate RTP caps. If this check fails, the win is voided and the Brain is asked to reissue a `NearMiss`.

6. **Atomic Settlement (`VaultService`):** Win credited in a single SQL transaction. Balance, `WageringProgress`, and `ShadowBalance` are all updated atomically. Failure at this stage rolls back completely — the player is neither charged nor credited.

7. **Audit & Broadcast:** The spin record is placed into the `IAuditBuffer` (non-blocking, memory queue). The `AuditWriterWorker` flushes it in bulk. A SignalR message is pushed to the client with the outcome, updated balance, and any animated events (jackpot triggers, quest completions, big win broadcasts).

---

## 🏗️ Deep Dive: The Trinity Architecture

The system's core logic is strictly decoupled into three independent layers, all residing in `AleaSim.Domain/Services/`. No layer knows the internal details of another — they communicate only through typed contracts.

---

### 1. 🏛️ The Vault — Financial Guard
*Primary file: `VaultService.cs`*

No money moves without the Vault's explicit approval. It is the single source of truth for all financial state.

**Core Responsibilities:**

- **Distributed Locking:** All balance-touching operations are wrapped with `await _lockService.AcquireLockAsync($"wallet_{userId}")`. The `RedisLockService` uses Redis SET NX (atomic set-if-not-exists) with a short TTL to guarantee mutual exclusion. The `InMemoryLockService` (`InMemoryLockService.cs`) provides an identical interface for single-node development/test environments — the two implementations are swapped via dependency injection.

- **Wallet Priority Order:** The Vault natively understands bonus funds. When processing a bet deduction it follows this strict order: (1) `BonusBalance`, (2) real `Balance`. If a bet straddles both wallets, it performs a split deduction and simultaneously updates `WageringProgress` to reflect how much bonus has been wagered.

- **Shadow Balance (`ShadowBalance` / `CanAffordWinCheck`):** Each user carries a `ShadowBalance` — a running total of their cumulative wins relative to total wagered (personal RTP). When an engine tries to credit a win, the Vault first evaluates whether crediting this amount would push the player's pRTP above the configured maximum AND whether the global `PoolBalance` has sufficient liquidity. If either check fails, the transaction is refused.

- **pRTP (Personal RTP) Tracking:** The Vault maintains per-user RTP stats (`TotalWagered`, `TotalPaid`) and computes the live pRTP delta. This feeds directly into the Brain's decision logic.

**Failure Scenario — What if the Vault rejects a win?**
The `VaultService` returns a typed `VaultResult` with a `CanCredit = false` flag and a reason code. `GameDirector` intercepts this and invokes `BrainService.Reissue()`, which constructs a `NearMiss` outcome (e.g., two-of-three matching symbols on a slot) — the player feels they just missed, not that the system refused their win.

---

### 2. 🧠 The Brain — AI Behavioral Engine
*Primary file: `BrainService.cs`*

The Brain does not roll dice. It makes **business decisions** and expresses them as `BrainDirective` objects that game engines must honour.

**Decision Input Matrix:**

| Input Signal | Source | Influence |
| :--- | :--- | :--- |
| `AvgSpinInterval` | Player behaviour profile | Flow State detection |
| `LossStreak` | Session history | Retention Hook trigger |
| `pRTP Delta` | VaultService stats | RTP Correction |
| `LTV Tier` | Player classification | Win magnitude scaling |
| Admin Overrides | Redis TTL keys | Absolute priority |

**Decision Tiers (Priority Order):**
1. **System Overrides** — Admin-set `force_win_{userId}` / `force_loss_{userId}` keys in Redis override everything else with absolute priority. TTL of 10 minutes.
2. **Global Shadow Mode** — If an admin activates `shadow_mode_global`, the Brain is bypassed for all players simultaneously, falling through to pure RNG.
3. **Retention Hooks ("Sugar Hits")** — Triggered when `LossStreak >= criticalThreshold`. Forces a win in the 10×–25× bet range to deliver a dopamine hit and reduce churn probability.
4. **Adaptive Volatility** — Adjusts win frequency/magnitude based on Flow State (see below).
5. **RTP Correction** — If player pRTP is running hot (above `GlobalTargetRtp`), Brain has a configurable probability of forcing a zero-win round.
6. **Normal RNG** — When none of the above apply, the Brain issues a `FreePlay` directive and the engine resolves outcomes via the `DeterministicRngService` (HMAC-SHA256).

**Flow State Detection:**

| State | `AvgSpinInterval` | Brain Response |
| :--- | :--- | :--- |
| 🔥 **Fast / "In The Zone"** | < 2.5 seconds | High volatility — fewer wins, larger multipliers |
| 😐 **Normal** | 2.5 – 7 seconds | Calibrated to target pRTP |
| 😴 **Slow / "Bored"** | > 7 seconds | "Popcorn Mode" — frequent small hits to re-engage |

**Near Miss Construction:** When the Brain cannot grant a win (Vault rejection or RTP budget exhausted), it passes a `NearMiss` instruction to the engine. The engine constructs a result that *looks* one symbol away from a big win — statistically neutral but psychologically impactful.

**What if the Brain fails or is unavailable?** The `GameDirector` wraps the Brain call in a try/catch. If `BrainService` throws (DB unavailable, timeout), the directive defaults to `FreePlay` and standard HMAC-SHA256 RNG resolves the outcome. The platform degrades gracefully.

---

### 3. ⚙️ The Engines — Deterministic Executors
*Primary files: `SlotGameEngine.cs`, `BlackjackGameEngine.cs`, `BaccaratGameEngine.cs`, `RouletteGameEngine.cs`, `DiceGameEngine.cs`*

Engines are **stateless workers** that receive a `BrainDirective` and produce a typed `GameResult`. They never talk to the database directly.

**Reverse Engineering (Core Mechanic):** For a directive requesting a $50 win, the engine does not spin randomly. It queries the in-memory paytable (a pre-computed dictionary of `{WinAmount → SymbolCombination[]}`), selects a combination worth exactly $50 (or the nearest achievable value), and constructs the reel stop positions to produce those symbols. The result is cryptographically signed by `DeterministicRngService` so the outcome can be independently audited.

**State Machines (CloverChase Sticky Respin):** When a Clover symbol lands during a base spin, the slot enters the Sticky Respin state. The engine serialises the current grid snapshot to JSON and writes it to Redis under `session.GameState.{userId}`. All future bets are locked to the respin stake. Even if the user closes the browser and returns an hour later, the engine deserialises the Redis snapshot and resumes from the exact frozen state — no spin is ever lost.

**BaseGameEngine (`BaseGameEngine.cs`):** All game engines inherit from `BaseGameEngine`, which hooks into `QuestService` (progress every spin), `LevelService` (XP grant), `JackpotService` (progressive accumulation), and `AuditBuffer` (non-blocking audit log). This ensures every game automatically participates in the RPG and jackpot systems without duplicating code.

---

## 🎰 Game Portfolio

### 🍀 CloverChase (Slot Machine)

> **[SCREENSHOT: CloverChase Full Grid]** *(Place image at `docs/images/cloverchase-grid.png`)*

A 5-reel × 4-row video slot with a layered bonus system. Rendered in the browser via optimized Canvas/JSInterop.

| Feature | Detail |
| :--- | :--- |
| Grid | 5×4 reels, 40 paylines |
| Wild Hierarchy | Standard Wild → Golden Wild → Expanding Wild |
| Sticky Respin | Each Clover locks in place; respins continue until no new Clovers land |
| Bell Bonus | Dedicated secondary game, triggered by 3+ Bell scatters |
| Golden Clover | Special symbol; awards multiplied win + enters Golden Respin sub-mode |
| Collect Coin | Collects all visible coin values on the grid in one payout |

<details>
<summary>📊 CloverChase Paytable Summary</summary>

| Symbol | 5-of-a-Kind | 4-of-a-Kind | 3-of-a-Kind |
| :--- | :--- | :--- | :--- |
| Golden Clover | 500× | 100× | 25× |
| Wild (substitutes all) | 200× | 50× | 10× |
| Bell | 100× | 30× | 8× |
| Clover | 50× | 15× | 5× |
| High Card (A/K/Q) | 20× | 8× | 2× |
| Low Card (J/10/9) | 10× | 4× | 1× |

</details>

---

### 💣 Fruit Blast (Cluster Slots / Cascade)

> **[SCREENSHOT: Fruit Blast Cascade]** *(Place image at `docs/images/fruit-blast.png`)*

A cluster-pays slot with an Avalanche/Cascade mechanic — winning symbols explode and new ones fall from above, potentially creating chain reactions.

| Feature | Detail |
| :--- | :--- |
| Mechanic | Cascading (winning clusters removed, symbols fall down) |
| TNT Bomb | Destroys a 3×3 area; triggered by 5-cluster of any fruit |
| Nuclear Bomb | Destroys a 5×5 area; escalates from TNT chain |
| Supernova Bomb | Full-grid wipe; triggered by 3 Nuclear Bombs in one cascade |
| Juice Meter | Fills with each cascade; at max, multiplier is applied to next win then resets |
| Target RTP | Configurable via paytable; Brain-corrected per session |

---

### ♠️ Blackjack (Table Game)

> **[SCREENSHOT: Blackjack Table]** *(Place image at `docs/images/blackjack-table.png`)*

Standard Vegas-rules Blackjack implemented in `BlackjackGameEngine.cs`.

- Soft and hard hand logic (Ace counted as 1 or 11)
- Split pairs (up to 3 times), Double Down on any first two cards
- Dealer stands on soft 17
- Insurance offered on dealer Ace
- House edge: ~0.5% with optimal play; Brain-adjusted pRTP applies on top

---

### 🃏 Baccarat (Table Game)

> **[SCREENSHOT: Baccarat Table]** *(Place image at `docs/images/baccarat.png`)*

Punto Banco rules as implemented in `BaccaratGameEngine.cs`.

- Player and Banker hands dealt per fixed drawing rules (no player decisions)
- Tie bet pays 8:1 (or 9:1 configurable); Banker bet charges 5% commission
- Brain RTP correction applies to outcome weighting, not card dealing — the draw rules are never violated

---

### 🎡 Roulette

> **[SCREENSHOT: Roulette Wheel]** *(Place image at `docs/images/roulette.png`)*

Implemented in `RouletteGameEngine.cs`.

- **European** (single-zero, 2.7% house edge) and **American** (double-zero, 5.26%) variants
- Full inside bets: Straight, Split, Street, Corner, Six Line
- Full outside bets: Red/Black, Even/Odd, Dozen, Column, High/Low
- Brain directives map to specific number outcomes via deterministic reverse engineering

---

### 🎲 DiceHub (Arcade)

> **[SCREENSHOT: DiceHub]** *(Place image at `docs/images/dicehub.png`)*

A fast-paced arcade-style dice game implemented in `DiceGameEngine.cs`.

- Instant outcomes — no animation wait, maximum throughput
- Player predicts Over/Under a threshold; configurable dice sides and multipliers
- Brain-driven streak management prevents extended losing runs
- Designed as a high-frequency game for audit throughput benchmarking

---

## 🧠 The Brain (AI Behavioral Engine) — Full Reference

*File: `BrainService.cs`*

### Decision Inputs

```csharp
// BehaviourProfile fields consumed by BrainService
decimal AvgSpinInterval      // Rolling average of last 10 spin gaps (seconds)
int     LossStreak           // Consecutive losses in current session
decimal pRtpDelta            // (TotalPaid / TotalWagered) - GlobalTargetRtp
string  LtvTier              // "Low" | "Medium" | "High" | "Whale"
```

### Decision Tiers (evaluated top-to-bottom, first match wins)

```
Tier 1: Admin Force Win/Loss  → Redis key "force_win_{userId}" (10 min TTL)
Tier 2: Global Shadow Mode    → Redis key "shadow_mode_global" (admin toggle)
Tier 3: Retention Hook        → LossStreak >= criticalThreshold
Tier 4: RTP Correction        → pRtpDelta > hotThreshold (50% probability trigger)
Tier 5: Adaptive Volatility   → Flow state modifies win distribution curve
Tier 6: Free Play (HMAC-RNG)  → No special condition met
```

### Retention Hook ("Sugar Hit") Logic

When `LossStreak >= criticalThreshold` (typically 8–12, configurable per LTV tier), the Brain:
1. Calculates a win target in the range `[10× bet, 25× bet]`, scaled by `LtvTier`
2. Emits a `BrainDirective { Type = RetentionHook, TargetMultiplier = X }`
3. Resets `LossStreak = 0` in the behaviour profile
4. The engine reverse-engineers a symbol combination matching the target

The Sugar Hit is intentionally designed to appear as natural luck — the player never knows the Brain intervened.

### RTP Correction Mechanism

```
If pRtpDelta > hotThreshold:
    Roll uniform random [0, 1]
    If roll < correctionProbability (default 0.50):
        Force ZeroWin directive (or NearMiss if LossStreak is also high)
    Else:
        Continue with adaptive volatility tier
```

### Admin Override Capabilities

Admins can inject Redis keys directly via the Admin Panel or CLI:
- `force_win_{userId}` — Forces next N spins to be wins (with configurable multiplier range)
- `force_loss_{userId}` — Forces next N spins to be losses (zero-win outcomes)
- `shadow_mode_global` — Bypasses Brain for all players (pure RNG mode)
- Both per-user keys expire after 10 minutes automatically via Redis TTL

---

## 🏛️ The Vault (Financial Guard) — Full Reference

*File: `VaultService.cs`*

### pRTP (Personal RTP) Tracking

Every user has running financial counters maintained by the Vault:

```csharp
decimal TotalWagered   // Lifetime total staked
decimal TotalPaid      // Lifetime total won
decimal pRTP           // = TotalPaid / TotalWagered (live ratio)
decimal ShadowBalance  // Net position (TotalPaid - TotalWagered); used for win capacity checks
```

The `pRTP` is compared against `GlobalTargetRtp` to compute `pRtpDelta`, which the Brain consumes.

### Shadow Balance and CanAffordWinCheck

Before crediting any win, the Vault runs:

```
CanCredit = (win <= GlobalPoolBalance * safetyFactor)
         AND (user.ShadowBalance + win <= maxShadowAllowance)
```

If either condition fails, `CanCredit = false`. The `GameDirector` then asks the Brain to reissue a non-win outcome.

### Distributed Locking

```csharp
// RedisLockService — Production
await using var @lock = await _lockService.AcquireLockAsync($"wallet_{userId}");
// Uses Redis SET NX EX (atomic, with TTL to prevent deadlocks)

// InMemoryLockService — Test/Dev fallback
// SemaphoreSlim per userId — identical interface, no Redis required
```

### Bonus Wallet Priority Order

```
1. BonusBalance  → deducted first; WageringProgress updated per deduction
2. Balance       → real money; only touched after BonusBalance is exhausted
3. Split Bet     → if BonusBalance < betAmount, remainder taken from Balance
```

### Faucet Security

The free-chips faucet (for new accounts and daily free spins) is protected by:
- Per-user Redis TTL key (`faucet_claimed_{userId}_{date}`)
- Server-side claim timestamp validation (replay protection)
- Rate limiting at the controller level

### Daily Loss Limit Enforcement

`GameDirector` checks `DailyLossLimit` before every spin:
- Reads `TodayLoss` from the user record (reset at 00:00 UTC by `DailyBonusBackgroundService`)
- If `TodayLoss + betAmount > DailyLossLimit`, the spin is rejected with HTTP 403 and a responsible gaming message

### Responsible Gaming Compliance

- Daily Loss Limit (user-configurable, admin-enforced maximum)
- Self-exclusion flag (permanent or timed)
- Reality check notifications (configurable time intervals)
- Session history transparency (all rounds auditable by user)

---

## 🛡️ Security & Provably Fair

### HttpOnly Cookie for RefreshToken

The JWT Refresh Token is never exposed to JavaScript. It is issued as an `HttpOnly; Secure; SameSite=Strict` cookie, making it inaccessible to XSS attacks. The Access Token (short-lived, 15 min) is held in memory on the client — never in `localStorage`.

### JWT Token Zombie Fix

Standard JWTs cannot be invalidated until expiry. AleaSim solves this via `OnTokenValidated` in `Program.cs`:

```
Every API request:
  1. Extract jti (JWT ID) from token claims
  2. Query Redis: GET "session_active:{sessionId}"
  3. If key is absent or mismatched → reject with 401
```

When an admin terminates a session or a user logs out, the Redis key is deleted. All in-flight requests using that token are immediately rejected — SignalR connections included.

### HMAC-SHA256 Provably Fair System

*File: `DeterministicRngService.cs`*

Four-phase cryptographic fairness protocol:

| Phase | Action |
| :--- | :--- |
| **1. Commitment** | Server generates `ServerSeed` (32 random bytes); shows player `SHA256(ServerSeed)` |
| **2. Entropy** | Player provides `ClientSeed` (free-text, default = random UUID) |
| **3. Execution** | `Result = HMAC_SHA256(key: ServerSeed, data: ClientSeed + ":" + Nonce)` |
| **4. Reveal** | Player rotates seed; server reveals previous `ServerSeed`; player can verify offline |

Because the Brain overrides some outcomes, the commitment applies to the *base RNG signal* — the Brain's directive is separately logged and auditable in the admin panel.

### DoS Protection

- **Rate Limiting:** ASP.NET Core rate limiting middleware at 60 req/min per IP (configurable)
- **Payload Limits:** Request body size caps enforced at the Kestrel level
- **Anti-bot Spin Throttle:** 300ms minimum between spins enforced server-side in `GameDirector`

### Input Validation

All DTOs are validated via Data Annotations + FluentValidation at the controller boundary. Bet amounts are validated against: min/max bet config, step granularity, and balance availability — before the Vault lock is even acquired.

### Session Hijacking Prevention

`GameDirector` binds each session to `(userId, sessionId)` tuple. Any request where the session token's embedded `userId` does not match the route/body `userId` is rejected with 401.

---

## 🎮 RPG Progression & Quest System

> **[SCREENSHOT: RPG Progression Screen]** *(Place image at `docs/images/rpg-progression.png`)*

### Player Levels & XP

Every spin, win, and quest completion awards XP, processed by `LevelService.cs`. The XP curve follows a geometric progression — early levels are fast to reward new players; higher levels require sustained engagement.

| Level Range | Title | Unlocks |
| :--- | :--- | :--- |
| 1–5 | Newcomer | Basic avatar frames |
| 6–15 | Regular | Daily bonus wheel access |
| 16–30 | Veteran | Weekly quest chains |
| 31–50 | High Roller | VIP cashback tier entry |
| 51+ | Legend | Custom avatar, exclusive tournaments |

### Quest Types

| Type | Reset | Example |
| :--- | :--- | :--- |
| **Daily** | 00:00 UTC | "Spin 50 times on CloverChase" |
| **Weekly** | Monday 00:00 UTC | "Win 5× your bet on Blackjack" |
| **Achievement** | One-time | "Trigger the Sticky Respin 10 times" |

### QuestService Integration

`QuestService.cs` is called from `BaseGameEngine` after every resolved round:

```csharp
await _questService.RecordProgressAsync(userId, gameType, outcome);
```

This single hook covers all games — no per-game quest wiring needed. On quest completion, the service emits a SignalR event to animate the reward notification in real time.

### Rewards

- **Avatars:** Unlocked at milestones or by completing achievement chains
- **Platform Perks:** Reduced wagering requirements on bonuses, faucet top-ups
- **Bonus Spins:** Credited to `BonusBalance` with configurable wagering requirement
- **Leaderboard Points:** Quest completions add score to the weekly XP leaderboard

### Leaderboard Integration

`LeaderboardService.cs` maintains sorted sets in Redis for real-time ranking. Leaderboards are exposed via a dedicated SignalR group — any rank change is pushed to all subscribed clients instantly.

---

## 💎 VIP, Cashback & Promotions

### VIP Tiers

| Tier | Monthly Wagered | Cashback Rate | Benefits |
| :--- | :--- | :--- | :--- |
| Bronze | < $5,000 | 2% | Daily bonus wheel |
| Silver | $5,000 – $20,000 | 5% | Dedicated support, faster withdrawals |
| Gold | $20,000 – $100,000 | 8% | Personal account manager, event invites |
| Platinum | $100,000+ | 12% | Custom limits, exclusive tournaments |

### Cashback System

Cashback accrues in real time based on net losses (not total wagered):

```
CashbackAccrued += max(0, LossAmount) * CashbackRate
```

`PromotionService.cs` tracks `CashbackBalance` separately from `BonusBalance`. Cashback is credited weekly by `DailyBonusBackgroundService` and carries no wagering requirement (it is real money).

### Daily Bonus Wheel

Available once per day (reset at 00:00 UTC). Spin the wheel for a random reward from a configurable prize table: bonus chips, free spins, XP boosts, raffle tickets, or jackpot seedings.

### Daily Streak Rewards

Consecutive login + activity streaks unlock escalating rewards:
- Day 3: 2× XP multiplier for 24 hours
- Day 7: Bonus spin bundle
- Day 14: VIP point acceleration
- Day 30: Tier upgrade consideration bonus

### ROI Tournament (Monthly)

Managed by `TournamentService.cs` and paid out by `TournamentPayoutBackgroundService.cs`.

- **Cadence:** Monthly (1st to last day)
- **Metric:** `ROI = ((TotalPaid - TotalWagered) / TotalWagered) × 100`
- **Scope:** Top 10 players by ROI over the month
- **Prize Pool:** Dynamic — base $25,000 + 1% of total platform wagers that month
- **Payout:** Automatic, idempotent batch credit on the 1st of the following month

### Raffle System

Managed by `RaffleBackgroundService.cs`.

- **Eligibility:** Active Players only (at least one real-money spin in the relevant period)
- **Ticket Accrual:** 1 raffle ticket per $50 wagered
- **Draw:** Weekly (minor) and Monthly (major) via recursive re-roll with verification
- **Anti-Manipulation:** Winner selection uses `DeterministicRngService`; result is seeded from the draw timestamp + platform entropy, independently auditable

---

## 📡 Real-Time & Social Features

> **[SCREENSHOT: Live Winners Feed & Chat]** *(Place image at `docs/images/social-realtime.png`)*

### SignalR + Redis Backplane

AleaSim uses ASP.NET Core **SignalR** for all real-time features. With multi-pod k3s deployment, a **Redis Backplane** (`StackExchange.Redis` SignalR provider) is used to route messages across all API pod instances — a message published on Pod A is received by a client connected to Pod B.

### Live Winners Feed

- Any win exceeding **100× the bet** automatically triggers a broadcast to **all connected clients**
- Broadcast payload: username (masked), game name, win amount, timestamp
- Feed is rendered as an animated ticker in the UI
- The threshold (100×) is configurable by admins without code changes

### Global & Private Chat

- **Global Room:** Open to all authenticated players; moderated in real time
- **Private Rooms:** Player-to-player or support channels
- **Moderation:** Admins can mute, kick, or ban users from chat directly from the Admin Panel; actions take effect immediately via SignalR disconnect

### Jackpot Celebration Animations

When a jackpot is claimed, a full-screen animation event is broadcast to all clients via SignalR. The event payload includes the jackpot tier, winner, and amount — fuelling social excitement without requiring a page reload.

---

## 💰 Multi-Jackpot System

> **[SCREENSHOT: Jackpot Tickers]** *(Place image at `docs/images/jackpot-tickers.png`)*

Managed by `JackpotService.cs`.

### Four Tiers

| Tier | Seed | Increment Rate | Typical Value |
| :--- | :--- | :--- | :--- |
| Mini | $50 | 0.5% of each bet | $50 – $500 |
| Minor | $500 | 1.0% of each bet | $500 – $5,000 |
| Major | $5,000 | 1.5% of each bet | $5,000 – $50,000 |
| Mega | $50,000 | 2.0% of each bet | $50,000+ |

### Progressive Accumulation

Every spin on every game increments all four jackpot pools simultaneously, proportional to the bet amount and the tier's increment rate. The accumulation happens in Redis (atomic INCRBYFLOAT) for zero-contention updates under high concurrency.

### Claim Security

Jackpot claims use a dedicated distributed lock: `jackpot_claim_{tier}`. This prevents two simultaneous winners in the (extremely rare) case of exact-same-millisecond triggers:

```
1. Acquire lock "jackpot_claim_{tier}"
2. Verify jackpot pool value has not changed since trigger
3. Credit winner atomically in SQL
4. Reset pool to seed value
5. Broadcast celebration event via SignalR
6. Release lock
```

### Real-Time Updates

Jackpot pool values are pushed to all connected clients every 10 seconds via SignalR, creating the "live ticking" visual effect in the UI without each client polling independently.

---

## 🤖 Automation (Background Workers)

All heavy background tasks are offloaded from the request pipeline to dedicated `IHostedService` workers in `AleaSim.Api/`.

### `AuditWriterBackgroundService.cs`

Database inserts are expensive under high concurrency. Every spin log bypasses SQL and goes into `IAuditBuffer` — a thread-safe in-memory queue. The worker wakes up on whichever comes first:

- **5-second timer** (flush whatever is in the buffer)
- **100-log threshold** (flush immediately when buffer fills)

This allows the platform to sustain thousands of spins per second with negligible SQL write latency on the hot path. On graceful shutdown, the buffer is force-flushed to prevent data loss.

### `SentinelBackgroundService.cs` (Integrity Police)

Runs every **10 minutes**:

1. **Balance Reconciliation:** For each active user, computes `SUM(Amount)` across all `Transactions` rows and compares it to `User.Balance`. Any discrepancy (even $0.01) triggers a `CriticalAnomaly` alert, freezes the affected account pending investigation, and pages the admin via SignalR.
2. **Anomaly Detection:** Flags accounts with statistically improbable win sequences (potential exploit attempts).
3. **RTP Stat Cleanup:** Purges orphaned pRTP calculation rows and compacts the behaviour profile history to keep query performance stable.

### `TournamentPayoutBackgroundService.cs`

Wakes up on the **1st of every month at 00:05 UTC**:

1. Queries all players' `TotalWagered` and `TotalPaid` for the concluded month
2. Calculates `ROI = ((TotalPaid - TotalWagered) / TotalWagered) × 100` per player
3. Ranks the Top 10
4. Calculates the dynamic prize pool: base `$25,000` + `1% × TotalPlatformWagers`
5. Credits each winner's `Balance` atomically
6. Sets an idempotency flag (`tournament_paid_{year}_{month}`) in Redis to prevent double-payout if the worker restarts mid-run

### `RaffleBackgroundService.cs`

Executes weekly (Sundays 23:50 UTC) and monthly (last day 23:50 UTC):

1. Filters eligible players (Active Player = at least one real-money spin in the period)
2. Builds a weighted ticket pool (1 ticket per $50 wagered)
3. Draws winner using `DeterministicRngService` — seeded from draw timestamp + server entropy
4. **Recursive re-roll:** If the drawn winner is ineligible (account frozen, self-excluded), the service re-draws automatically up to a configurable maximum retry count
5. Credits the prize to the winner's `Balance` and broadcasts the result

### `DailyBonusBackgroundService.cs`

Runs at **00:00 UTC daily**:

1. Resets `TodayLoss` for all users (Daily Loss Limit counter)
2. Recalculates `DailyStreakDays` for users who logged in and played the previous day
3. Credits streak milestone rewards where applicable
4. Aggregates weekly cashback totals (on Mondays) and credits `CashbackBalance`
5. Marks the previous day's `DailyBonusWheel` as available for users to spin

---

## 🖥️ Admin Panel

> **[SCREENSHOT: Admin Panel Dashboard]** *(Place image at `docs/images/admin-panel-full.png`)*

The Admin Panel (`AdminService.cs` + dedicated admin Blazor pages) provides live operational control without requiring code deployments or server restarts.

### Per-User Directives

| Control | Mechanism | TTL |
| :--- | :--- | :--- |
| Force Win | Redis key `force_win_{userId}` with multiplier range | 10 minutes |
| Force Loss | Redis key `force_loss_{userId}` | 10 minutes |
| Session Termination | Delete Redis `session_active:{sessionId}` | Immediate |
| Account Freeze | DB flag + all active sessions terminated | Until unfrozen |

### Global Controls

- **Shadow Mode:** Toggle `shadow_mode_global` in Redis — Brain bypassed, pure HMAC-RNG for all players instantly
- **Maintenance Mode:** Gracefully rejects new bets while in-flight rounds complete
- **Dynamic Paytable:** Slot pay tables and RTP targets can be adjusted at runtime via the Admin Panel; changes take effect on the next spin without service restart

### Real-Time RTP Dashboard

The admin panel streams live pRTP per game, per user segment, and platform-wide via SignalR. Graphs update every 30 seconds. Admins can drill into any user's full audit trail.

### Financial Pool Monitoring

- Live `PoolBalance` vs. total outstanding `ShadowBalance`
- Jackpot pool levels (all four tiers)
- Pending bonus liability (total `BonusBalance` across all users)
- Daily win/loss summary

---

## ⌨️ CLI Tool (AleaSim.CLI)

The `AleaSim.CLI` project is a command-line administrative console providing direct access to simulation and analysis capabilities without needing a browser.

### Purpose

- Run **Brain behaviour simulations** against configurable player profiles
- Stress-test the Vault's locking and pRTP correction at scale
- Replay audit logs to verify outcome distributions
- Seed development databases with realistic player histories

### Simulation Center Capabilities

```bash
# Run a simulation of 10,000 spins for a given player profile
dotnet run --project AleaSim.CLI simulate --player high-roller --spins 10000 --game cloverchase

# Analyse RTP distribution from the last 30 days of audit logs
dotnet run --project AleaSim.CLI analyze-rtp --days 30

# Force a Brain directive test (dry-run, no DB writes)
dotnet run --project AleaSim.CLI brain-test --userId {guid} --directive RetentionHook
```

> [!TIP]
> The CLI is the fastest way to validate Brain behaviour changes — run a 100,000-spin simulation in seconds and inspect the directive distribution histogram.

---

## 🔬 Testing

*Suite: `AleaSim.Tests/` — xUnit*

### Run the Test Suite

```bash
dotnet test AleaSim.Tests/
```

### Test Categories

| Category | What it Covers |
| :--- | :--- |
| **Vault Integrity** | Race condition simulations — 50 parallel bet requests, balance reconciliation, lock acquisition/release ordering |
| **RNG Distribution** | Validates HMAC-SHA256 output distribution; Chi-squared test against uniform expectation |
| **Brain Directive Logic** | Unit tests for each decision tier; ensures tier priority order is respected |
| **Game Engine Reverse Engineering** | Verifies that for any requested win amount, the engine produces a symbol combination whose paytable value equals the target |
| **Background Worker Idempotency** | Verifies `TournamentPayoutWorker` does not double-pay when run twice with the same month flag |

> [!NOTE]
> The Vault Integrity tests deliberately launch concurrent `Task[]` arrays against the same `userId` to prove the distributed locking prevents balance corruption under contention.

---

## 📚 Documentation & Deep Dive

All architectural, mathematical, and design documents live in `GameDesign/`.

### Architecture & Flow
| Document | Description |
| :--- | :--- |
| 🏗️ [The Trinity Architecture](GameDesign/Core_Logic/The_Trinity_Architecture.md) | Full Brain/Vault/Engine architecture |
| 🔒 [Security Specifications](GameDesign/Core_Logic/Security_Specifications.md) | JWT, HMAC, locking, anti-abuse |
| 🗂️ [Architecture Overview](GameDesign/Core_Logic/Architecture.md) | System-level component diagram |

### Economy & Mathematics
| Document | Description |
| :--- | :--- |
| 🧠 [The Brain Intelligence](GameDesign/Core_Logic/The_Brain_Intelligence.md) | Full Brain decision model, all tiers |
| 🏦 [Financial Vault & RTP](GameDesign/Core_Logic/Financial_Vault_RTP.md) | pRTP, ShadowBalance, CanAffordWin math |

### Games
| Document | Description |
| :--- | :--- |
| 🍀 [CloverChase Logic](GameDesign/Games/CloverChase.md) | Engine mechanics, state machine |
| 📋 [CloverChase Rules](GameDesign/Games/CloverChase_Rules_Updated.md) | Full player-facing rules |
| 💣 [FruitBlast Design](GameDesign/Games/FruitBlast_Design.md) | Cascade mechanic, bomb types |

### Core Logic & Systems
| Document | Description |
| :--- | :--- |
| ⚡ [Optimization & Scripting](GameDesign/Core_Logic/OptimizationAndScripting.md) | JS interop, Canvas rendering, perf |
| ⚙️ [Automation Specs](GameDesign/Core_Logic/Automation_Specs.md) | All background worker specifications |
| 🎁 [Promotions & Tournaments](GameDesign/Core_Logic/Promotions_And_Tournaments.md) | Full VIP, cashback, raffle rules |
| 🔧 [CMS Reverse Engineering](GameDesign/Core_Logic/CMS_Reverse_Engineering.md) | Outcome construction deep-dive |

### Deployment
| Document | Description |
| :--- | :--- |
| ☸️ k3s Deployment Guide | `docs/k3s/deployment-guide.md` (see Setup section) |

---

## 📂 Project Structure Map

```text
AleaSim/
├── AleaSim.Api/
│   ├── Controllers/          # HTTP endpoints: GameController, AuthController, AdminController
│   ├── Hubs/                 # SignalR: GameHub, ChatHub, LiveFeedHub
│   ├── Workers/              # Background services: Audit, Sentinel, Tournament, Raffle, DailyBonus
│   ├── Middleware/           # Rate limiting, error handling, JWT validation hooks
│   └── Program.cs            # DI composition root, Kestrel config, SignalR backplane setup
│
├── AleaSim.Client/
│   ├── Pages/                # Blazor WASM pages: Index, Games, Profile, Admin, Leaderboard
│   ├── Components/           # Reusable UI components (MudBlazor + custom)
│   ├── wwwroot/js/           # JavaScript interop modules: Canvas renderers, animation engines
│   └── Services/             # Client-side state: SignalR client, local game state cache
│
├── AleaSim.Domain/
│   ├── Services/
│   │   ├── BrainService.cs             # AI behavioral engine
│   │   ├── VaultService.cs             # Financial guard & RTP tracking
│   │   ├── GameDirector.cs             # Orchestrates the full spin lifecycle
│   │   ├── BaseGameEngine.cs           # Shared hooks: quests, XP, jackpot, audit
│   │   ├── SlotGameEngine.cs           # CloverChase + FruitBlast
│   │   ├── BlackjackGameEngine.cs      # Blackjack
│   │   ├── BaccaratGameEngine.cs       # Baccarat
│   │   ├── RouletteGameEngine.cs       # Roulette
│   │   ├── DiceGameEngine.cs           # DiceHub arcade
│   │   ├── DeterministicRngService.cs  # HMAC-SHA256 provably fair RNG
│   │   ├── JackpotService.cs           # Progressive jackpot accumulation & claims
│   │   ├── QuestService.cs             # RPG quest progress hooks
│   │   ├── LevelService.cs             # XP & levelling
│   │   ├── LeaderboardService.cs       # Redis sorted-set leaderboards
│   │   ├── TournamentService.cs        # ROI tournament calculations
│   │   ├── PromotionService.cs         # Cashback, VIP, bonus wheels
│   │   ├── AdminService.cs             # Admin directives & panel backend
│   │   ├── AuditService.cs             # Audit log management
│   │   ├── RedisLockService.cs         # Production distributed locking
│   │   ├── InMemoryLockService.cs      # Dev/test locking fallback
│   │   ├── RedisCacheService.cs        # Typed Redis cache wrapper
│   │   └── SimulationService.cs        # CLI simulation engine
│   ├── Entities/             # EF Core entity models (User, Transaction, Jackpot, Quest, …)
│   ├── Enums/                # GameType, DecisionType, LtvTier, QuestType, …
│   ├── Interfaces/           # ILockService, IAuditBuffer, IBrainService, IVaultService, …
│   └── Models/               # Value objects: BrainDirective, VaultResult, GameResult, …
│
├── AleaSim.Persistence/
│   ├── DbContext/            # EF Core DbContext + entity configurations
│   ├── Migrations/           # EF Core migration history
│   └── Repositories/        # Dapper-based read-optimized query repos
│
├── AleaSim.Shared/
│   ├── DTOs/                 # Request/response DTOs shared by API + Client
│   └── Enums/                # Shared enums (GameType, etc.)
│
├── AleaSim.CLI/
│   ├── Commands/             # CLI command handlers (simulate, analyze-rtp, brain-test)
│   └── Program.cs            # CLI entry point
│
├── AleaSim.Tests/
│   ├── VaultTests/           # Race condition, balance reconciliation, lock tests
│   ├── RngTests/             # Distribution, HMAC, seed rotation tests
│   ├── BrainTests/           # Directive tier priority, retention hook, RTP correction
│   └── EngineTests/          # Reverse engineering correctness, paytable integrity
│
├── GameDesign/
│   ├── Core_Logic/           # Architecture, Security, Brain, Vault, Automation, Promotions docs
│   └── Games/                # Per-game design documents (CloverChase, FruitBlast, …)
│
└── docs/
    ├── images/               # Screenshots and diagrams (banner.png, k3s-cluster-diagram.png, …)
    └── k3s/                  # Kubernetes manifests and deployment guide
```

---

## 🚀 Setup & Installation Guide

### Local Development

#### Prerequisites

| Requirement | Version | Notes |
| :--- | :--- | :--- |
| .NET SDK | 8.0+ | [Download](https://dotnet.microsoft.com/download) |
| MySQL / MariaDB | 8.0+ | Or configure for SQL Server |
| Redis | 6.0+ | Mandatory — SignalR backplane + locking |
| Node.js | 18+ | Optional — only for JS asset bundling |

#### 1. Configuration (`appsettings.Development.json`)

Place this in `AleaSim.Api/`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=aleasim;Uid=root;Pwd=yourpassword;",
    "Redis": "localhost:6379,password=yourredispassword"
  },
  "Jwt": {
    "Key": "YOUR_256BIT_SECRET_KEY_HERE_MINIMUM_32_CHARS",
    "Issuer": "AleaSim",
    "Audience": "AleaSim",
    "ExpiryMinutes": 15,
    "RefreshExpiryDays": 7
  },
  "Admin": {
    "Id": "YOUR-ADMIN-GUID-HERE"
  },
  "Casino": {
    "GlobalTargetRtp": 0.96,
    "PoolBalance": 1000000.00,
    "DailyLossLimitDefault": 500.00
  }
}
```

| Key | Description |
| :--- | :--- |
| `ConnectionStrings:DefaultConnection` | MySQL/MariaDB connection string |
| `ConnectionStrings:Redis` | Redis endpoint with auth |
| `Jwt:Key` | ≥256-bit HMAC signing key — **never commit to source control** |
| `Admin:Id` | GUID of the bootstrap admin account |
| `Casino:GlobalTargetRtp` | Platform RTP target (e.g., `0.96` = 96%) |
| `Casino:PoolBalance` | Starting casino liquidity pool |
| `Casino:DailyLossLimitDefault` | Default daily loss cap per user |

> [!WARNING]
> Never commit real `Jwt:Key` values or database credentials. Use environment variables or Kubernetes Secrets in production.

#### 2. Database Initialization

```bash
# Apply EF Core migrations
dotnet ef database update --project AleaSim.Persistence --startup-project AleaSim.Api
```

#### 3. Run Locally

```bash
# Terminal 1 — API + Workers
cd AleaSim.Api && dotnet run

# Terminal 2 — Blazor WASM Client
cd AleaSim.Client && dotnet run
```

Navigate to `https://localhost:5001` (or the port shown in your console).

---

### 🐳 Docker Compose (Quick Start)

> [!TIP]
> Docker Compose is the fastest way to run the full stack locally without installing MySQL or Redis manually.

```bash
# From the repository root
docker compose up --build
```

This starts: `aleasim-api`, `aleasim-client`, `mysql`, `redis` — all networked together. The client is available at `http://localhost:8080`.

---

### ☸️ Kubernetes / k3s Deployment

#### Prerequisites

- A running k3s cluster (`k3s server` / `k3s agent` nodes)
- `kubectl` configured to target the cluster
- A container registry accessible from the cluster nodes (e.g., local registry or Docker Hub)

#### Build & Push Docker Images

```bash
# Build API image
docker build -t your-registry/aleasim-api:latest -f AleaSim.Api/Dockerfile .

# Build Client image
docker build -t your-registry/aleasim-client:latest -f AleaSim.Client/Dockerfile .

# Push
docker push your-registry/aleasim-api:latest
docker push your-registry/aleasim-client:latest
```

#### Apply Manifests

```bash
# Create namespace
kubectl apply -f docs/k3s/namespace.yaml

# Apply secrets (connection strings, JWT key)
kubectl apply -f docs/k3s/secrets.yaml

# Deploy all services
kubectl apply -f docs/k3s/
```

#### Check Pod Status

```bash
kubectl get pods -n aleasim
# Expected: aleasim-api, aleasim-client, redis, mysql all Running

kubectl logs -n aleasim deployment/aleasim-api --tail=50
```

> [!NOTE]
> Full manifest files, Ingress configuration, and TLS setup instructions are in `docs/k3s/`. See [docs/k3s/deployment-guide.md](docs/k3s/deployment-guide.md).

---

## 📝 License & Credits

```
MIT License — See LICENSE file for full terms.
```

**AleaSim** is an independent engineering project built to explore:
- Behaviorally-driven game mathematics and adaptive RTP systems
- High-concurrency financial transaction safety patterns
- Real-time multi-client architectures with SignalR + Redis
- Full-stack .NET 8 application design from domain to deployment

> [!NOTE]
> AleaSim is a **simulation and research platform**. It is not a licensed gambling product. It does not handle real currency. All financial figures shown are simulated credits with no real-world value.

---

*© 2026 AleaSim Entertainment — Provably Fair · AI-Driven · Kubernetes-Deployed*
