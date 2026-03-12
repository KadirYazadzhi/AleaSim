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

## ⚙️ The Request Lifecycle: What happens when you click "Spin"?

The platform doesn't just generate a random number. A single HTTP POST to `GameController.cs` triggers a complex orchestration process managed by `GameDirector.cs`:

1. **Security & Rate Limiting (`GameDirector`):** The system first validates the `sessionId` against the `userId` to prevent Session Hijacking. It then checks `LastBetTimestamp`. If spins are faster than 300ms, it blocks the request. It also enforces Responsible Gaming limits (`DailyLossLimit`).
2. **Financial Lock (`VaultService`):** Before anything spins, the `VaultService` applies a Distributed Lock in Redis (`wallet_{userId}`). This prevents "Double Spend" race conditions.
3. **Behavioral Analysis (`BrainService`):** The Brain analyzes the player's history (loss streaks, average spin speed) and issues a `BrainDirective` (e.g., "Give this player a 20x win to prevent them from leaving").
4. **Reverse Engineering (`GameEngine`):** The specific game engine (e.g., `SlotGameEngine`) receives the directive, opens its `Paytable`, and calculates *which* symbols need to land on the reels to equal that exact 20x win.
5. **Atomic Settlement (`VaultService`):** The win is approved and settled atomically in the SQL database, and a real-time SignalR update is pushed to the client UI.

---

## 🏗️ The Trinity Architecture

The system's core logic is strictly decoupled into three independent layers. No layer knows the internal details of another — they communicate only through typed contracts.

### 1. 🏛️ The Vault (Financial Guard)
No money moves without the Vault's explicit approval. It is the single source of truth for all financial state.
* **Distributed Locking:** All operations are wrapped via Redis `SET NX` locks (`wallet_{userId}`) to guarantee mutual exclusion under high concurrency.
* **Shadow Balance & RTP Checks:** Before crediting any win, the Vault checks if the payout violates the user's personal RTP limits or the global liquidity pool. If a win is too large, the Vault **rejects it**, forcing the system to generate a safe "Near Miss" instead.
* **Wallet Priority:** Automatically handles split deductions between `BonusBalance` and real `Balance`, updating wagering requirements atomically.

👉 *Deep dive: [Financial Vault & RTP](GameDesign/Core_Logic/Financial_Vault_RTP.md)*

### 2. 🧠 The Brain (AI Behavioral Engine)
The Brain does not roll dice; it makes business decisions. It analyzes a player's `BehaviourProfile` (loss streaks, average spin interval, pRTP delta, LTV tier) and issues absolute directives.
* **Flow State Detection:** Adjusts volatility. If a player is spinning fast ("In the Zone"), it shifts to fewer wins but larger multipliers. If they are slow, it enters "Popcorn Mode" (frequent small hits).
* **Retention Hooks ("Sugar Hits"):** If a player hits a critical loss streak, the Brain forces a win in the 10×–25× range to deliver dopamine and reduce churn probability.
* **Admin Overrides:** Admins can inject Redis keys (e.g., `force_win_{userId}`) to override the Brain in real-time.

👉 *Deep dive: [The Brain Intelligence](GameDesign/Core_Logic/The_Brain_Intelligence.md)*

### 3. ⚙️ The Engines (Deterministic Executors)
Engines are stateless workers. Instead of spinning randomly, they perform **Reverse Engineering**. 
If the Brain requests a $50 win, the engine consults the paytable, selects a combination worth exactly $50, and mathematically forces the reel strips or card deck to produce that exact outcome. This is signed via `DeterministicRngService` (HMAC-SHA256) for provable fairness.

👉 *Deep dive: [The Trinity Architecture](GameDesign/Core_Logic/The_Trinity_Architecture.md)*

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

## 🛡️ Security & Provably Fair

### JWT & Session Management
* **HttpOnly Tokens:** The JWT Refresh Token is issued as an `HttpOnly; Secure; SameSite=Strict` cookie, completely inaccessible to JavaScript / XSS.
* **JWT Zombie Fix:** Standard JWTs cannot be invalidated until expiry. AleaSim solves this via a custom `OnTokenValidated` hook checking a Redis `session_active` key. If an admin terminates a session, the token is instantly rejected mid-flight.
* **Hijack Prevention:** `GameDirector` strictly binds each session to a `(userId, sessionId)` tuple.

### HMAC-SHA256 Provably Fair System
Four-phase cryptographic fairness protocol (`DeterministicRngService.cs`):

| Phase | Action |
| :--- | :--- |
| **Commitment** | Server generates `ServerSeed` (32 random bytes); shows player `SHA256(ServerSeed)` |
| **Entropy** | Player provides `ClientSeed` (free-text, default = random UUID) |
| **Execution** | `Result = HMAC_SHA256(key: ServerSeed, data: ClientSeed + ":" + Nonce)` |
| **Reveal** | Player rotates seed; server reveals previous `ServerSeed` for offline verification |

*Note: When the Brain overrides outcomes (e.g., Retention Hooks), the commitment applies to the base RNG signal. Brain directives are separately logged and auditable.*

### DoS & Abuse Protection
* **Anti-bot Throttle:** 300ms minimum between spins enforced server-side.
* **Rate Limiting:** ASP.NET Core middleware at 60 req/min per IP.

---

## 🤖 Automation (Background Workers)

Heavy processing is offloaded from the hot path using ASP.NET Core `IHostedService` workers:

* **`AuditWriterWorker`:** Spin logs bypass SQL and enter an `IAuditBuffer` (thread-safe in-memory queue). Flushed in bulk every 5s or 100 logs, sustaining thousands of TPS.
* **`SentinelWorker` (Integrity Police):** Runs every 10 minutes to mathematically reconcile `SUM(Transactions)` against `User.Balance`. Any $0.01 discrepancy immediately freezes the account and alerts admins via SignalR.
* **`TournamentPayoutWorker`:** Idempotent monthly worker that computes player ROI and distributes dynamic prize pools. Uses Redis flags to prevent double-payouts on service restarts.
* **`RaffleWorker`:** Uses HMAC RNG to draw tickets, featuring automatic recursive re-rolls if an excluded account is drawn.

---

## 🛠️ Live Ops: Admin Panel & CLI

* **Admin Panel (SignalR):** Provides real-time operational control. Admins can inject Redis directives (`force_win`, `force_loss`), toggle global Shadow Mode (bypassing the Brain), and stream live platform RTP graphs.
* **AleaSim.CLI:** A terminal tool for dry-running simulations and analyzing payout distributions:
  ```bash
    # Run a simulation of 10,000 spins for a given player profile
    dotnet run --project AleaSim.CLI simulate --player high-roller --spins 10000 --game cloverchase
  ```
  
---

## 🔬 Testing Strategy

*(Suite: `AleaSim.Tests/` — xUnit)*

* **Vault Integrity:** Deliberately launches concurrent `Task[]` arrays against the same user balance to prove the Redis distributed locking prevents corruption under severe race conditions.
* **RNG Distribution:** Chi-squared tests validating HMAC-SHA256 uniform distribution over large sample sizes.
* **Engine Reverse Engineering:** Verifies that engines mathematically construct valid paytable combinations for any forced Brain target.
* **Worker Idempotency:** Asserts that background payout workers do not double-process when triggered multiple times for the same period.

---

## 📚 Documentation & Deep Dive

All architectural, mathematical, and design documents live in `GameDesign/`.

| Category | Key Documents |
| --- | --- |
| **Architecture** | [The Trinity Architecture](GameDesign/Core_Logic/The_Trinity_Architecture.md) • [Architecture Diagram](GameDesign/Core_Logic/Architecture.md) |
| **Economy & AI** | [The Brain Intelligence](GameDesign/Core_Logic/The_Brain_Intelligence.md) • [Financial Vault & RTP](GameDesign/Core_Logic/Financial_Vault_RTP.md) |
| **Security & Ops** | [Security Specs](GameDesign/Core_Logic/Security_Specifications.md) • [Automation Specs](GameDesign/Core_Logic/Automation_Specs.md) |
| **Game Math** | [CloverChase](GameDesign/Games/CloverChase.md) • [FruitBlast](GameDesign/Games/FruitBlast_Design.md) • [Roulette](GameDesign/Games/Roulette_Design.md) |

---

## 📂 Project Structure Map

<details>
<summary><b>Click to expand project structure</b></summary>

```text
AleaSim/
├── AleaSim.Api/              # HTTP API, SignalR Hubs, Background Workers, Middleware
├── AleaSim.Client/           # Blazor WASM UI, Canvas rendering JS, State services
├── AleaSim.Domain/
│   ├── Services/             # Brain, Vault, Engines, RNG, Redis Locks, RPG Systems
│   ├── Entities/             # EF Core Models
│   └── Interfaces/           # Strict contracts
├── AleaSim.Persistence/      # EF Core DbContext, Migrations, Dapper read repos
├── AleaSim.Shared/           # DTOs and Enums shared via Blazor
├── AleaSim.CLI/              # Terminal simulation engine
├── AleaSim.Tests/            # xUnit tests for race conditions, RNG, and game math
├── GameDesign/               # Full mathematical and architectural documentation
└── docs/                     # k3s manifests, screenshots

```

</details>

---

## 🚀 Setup & Installation Guide

### 🐳 Docker Compose (Quick Start - Recommended)

The fastest way to run the full stack (API, Blazor Client, MySQL, Redis) locally:

```bash
docker compose up --build

```

*The client will be available at `http://localhost:8080`.*

### 💻 Local Development

**Prerequisites:** .NET 8 SDK, MySQL 8.0+, Redis 6.0+

1. Update `AleaSim.Api/appsettings.Development.json` with your DB and Redis connection strings.
2. Apply EF Core migrations:
```bash
dotnet ef database update --project AleaSim.Persistence --startup-project AleaSim.Api

```


3. Run the API and Client in separate terminals:
```bash
cd AleaSim.Api && dotnet run
cd AleaSim.Client && dotnet run

```



### ☸️ Kubernetes / k3s Deployment

AleaSim is designed for a multi-pod Kubernetes environment. Full manifests and an ingress configuration guide are available in `docs/k3s/`.

```bash
kubectl apply -f docs/k3s/namespace.yaml
kubectl apply -f docs/k3s/secrets.yaml
kubectl apply -f docs/k3s/

```

👉 *See [k3s Deployment Guide](https://www.google.com/search?q=docs/k3s/deployment-guide.md) for full instructions.*

---

## 📝 License & Credits

```
MIT License — See LICENSE file for full terms.

```

**AleaSim** is an independent engineering project built to explore behaviorally-driven game mathematics, high-concurrency financial transaction safety, and full-stack .NET 8 application design.

> [!NOTE]
> AleaSim is a **simulation and research platform**. It is not a licensed gambling product. It does not handle real currency. All financial figures shown are simulated credits with no real-world value.

---

*© 2026 AleaSim Entertainment — Provably Fair · AI-Driven · Kubernetes-Deployed*
