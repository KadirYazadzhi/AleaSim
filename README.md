# 🎰 AleaSim: The Trinity Casino Architecture

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WASM-5C2D91?style=flat-square&logo=blazor)
![Redis](https://img.shields.io/badge/Redis-Active-DC382D?style=flat-square&logo=redis)
![Security](https://img.shields.io/badge/Security-Production--Ready-success?style=flat-square)
![Provably Fair](https://img.shields.io/badge/RNG-HMAC--SHA256-orange?style=flat-square)

**AleaSim** is a high-performance, enterprise-grade gambling simulation platform built on the **Trinity Architecture**. It moves beyond standard RNG, implementing a **Data-Driven Behavioral Engine** where every outcome is mathematically deterministic, cryptographically verifiable, and AI-optimized to maximize player retention. 

It merges classic gambling mechanics with a deep RPG player progression system, real-time social elements, and a complex microservice-oriented backend.

> [!IMPORTANT]
> **[SCREENSHOT: Main User Dashboard / Banner]**
> *(Place your main platform banner in `docs/images/banner.png`)*

---

## 📸 Gallery & UI

| Dashboard / Home | Gameplay (Clover Chase / Roulette) |
| :---: | :---: |
| ![Dashboard](docs/images/dashboard.png) | ![Gameplay](docs/images/gameplay.png) |
| **RPG Progression & Quests** | **Admin Panel & RTP Control** |
| ![Progression](docs/images/progression.png) | ![Admin Panel](docs/images/admin-panel.png) |

---

## ⚙️ The Request Lifecycle: What happens when you click "Spin"?

The platform doesn't just generate a random number. A single HTTP POST to `GameController.cs` triggers a complex orchestration process managed by `GameDirector.cs`:

1. **Security & Rate Limiting (`GameDirector`):** The system first validates the `sessionId` against the `userId` to prevent Session Hijacking. It then checks `LastBetTimestamp`. If spins are faster than 300ms, it blocks the request. It also enforces Responsible Gaming limits (`DailyLossLimit`).
2. **Financial Lock (`VaultService`):** Before anything spins, the `VaultService` applies a Distributed Lock in Redis (`wallet_{userId}`). This prevents "Double Spend" race conditions.
3. **Behavioral Analysis (`BrainService`):** The Brain analyzes the player's history (loss streaks, average spin speed) and issues a `BrainDirective` (e.g., "Give this player a 20x win to prevent them from leaving").
4. **Reverse Engineering (`GameEngine`):** The specific game engine (e.g., `SlotGameEngine`) receives the directive, opens its `Paytable`, and calculates *which* symbols need to land on the reels to equal that exact 20x win.
5. **Atomic Settlement (`VaultService`):** The win is approved and settled atomically in the SQL database, and a real-time SignalR update is pushed to the client UI.

---

## 🏗️ Deep Dive: The Trinity Architecture

The system's core logic is strictly decoupled into three layers located in `AleaSim.Domain/Services/`.

### 1. 🏛️ The Vault (The Financial Guard)
*File: `VaultService.cs`*

No money moves without the Vault's approval. It protects the casino's house edge.
* **Distributed Locking:** Uses `await _lockService.AcquireLockAsync($"wallet_{userId}")` for all transactions. If a malicious user sends 10 parallel bet requests, 9 will wait until the first is resolved, preventing balance manipulation.
* **Wallet Priority:** The Vault natively supports Bonus Funds. It deducts from `BonusBalance` first. If insufficient, it splits the bet, taking the remainder from real `Balance`, while simultaneously updating the `WageringProgress`.
* **The Shadow Check (`CanAffordWinCheck`):** When a game tries to pay a player, the Vault checks the global `PoolBalance` AND the user's `ShadowBalance` (accumulated RTP). If the system cannot afford the win, the transaction is rejected, and the Brain must generate a "Near Miss" instead.

### 2. 🧠 The Brain (The AI & Psychology)
*File: `BrainService.cs`*

The Brain does not roll dice; it makes business decisions. It outputs `DecisionType` directives.
* **Flow State Detection:** It tracks `AvgSpinInterval`. If a player spins rapidly (< 2.5s), they are "In the Zone", and the Brain increases volatility (fewer but bigger wins). If they spin slowly (> 7s), they are bored, and the Brain switches to "Popcorn Mode" (frequent, small hits) to re-engage them.
* **Retention Hooks:** If a player hits a critical loss streak (`profile.LossStreak >= threshold`), the Brain intervenes. It forces a `RetentionHook` directive, generating a 10x-25x win to deliver a dopamine hit and prevent churn.
* **RTP Correction:** If a player is winning significantly above the casino's `GlobalTargetRtp`, the Brain has a 50% chance to force a losing round or a zero-win outcome to cool them down.

### 3. ⚙️ The Engines (The Executors)
*Files: `SlotGameEngine.cs`, `BlackjackGameEngine.cs`, etc.*

* **Clover Chase (Slots):** Features a complex state machine for its "Sticky Respin" mechanic. If a Clover lands, the state is serialized to JSON and stored in Redis (`session.GameState`). Future bets are locked, and the engine resumes the exact visual state even if the user refreshes their browser.
* **Reverse Engineering:** For a requested $50 win, the engine doesn't spin randomly. It looks up the paytable, finds a combination worth $50, and mathematically forces the reel strips to stop on those exact symbols.

---

## 🛡️ Security & Provably Fair

### Token Zombie Fix (JWT Revocation)
Standard JWTs cannot be invalidated until they expire. AleaSim solves this in `Program.cs` via `OnTokenValidated`. Every API request extracts the token's `jti` (unique ID) and checks `$"session_active:{sessionId}"` in **Redis**. If an admin terminates a session or a user logs out, the token is instantly rejected, severing all SignalR and API connections.

### Cryptographic Fairness (HMAC-SHA256)
*File: `RngService.cs`*

Because the Brain overrides standard RNG, AleaSim uses cryptographic commitments to ensure the casino cannot cheat.
* **Commitment:** The server generates a 32-byte `ServerSeed` and shows the user its SHA-256 hash.
* **Entropy:** The user provides a `ClientSeed`.
* **Execution:** Outcomes are generated using: `Result = HMAC_SHA256(ServerSeed, ClientSeed + Nonce)`. 
Users can rotate their seed at any time to reveal the old server seed and independently verify past game math.

---

## 🚀 Player Experience & Core Features

* **Game Portfolio:** Includes Slots (CloverChase), Blackjack, Baccarat, Roulette, and fast-paced Arcade games (DiceHub). Rendered using highly optimized Canvas/JSInterop.
* **RPG & Quests:** Integrated directly into `BaseGameEngine`. Every spin increments progress for daily missions ("Spin 100 times") via the `QuestService`. Leveling up unlocks avatars and specific platform perks.
* **Social & Real-Time:** * Big wins (>100x) are broadcasted over the SignalR Backplane to the **Live Winners Feed** across all connected clients.
  * Global & Private Chat rooms with real-time moderation.
* **Multi-Jackpot System:** Mini, Minor, Major, and Mega progressive jackpots accumulating in real-time.

---

## 🤖 Automation (Background Workers)

To maintain high performance, heavy tasks are offloaded to background services (`AleaSim.Api/Workers/`).

1. **`AuditWriterWorker`:** Database inserts are slow. Instead of writing every spin to SQL immediately, logs go into an `IAuditBuffer` (memory). This worker wakes up every 5 seconds (or 100 logs) and performs a mass `Bulk Insert`. This allows the platform to handle thousands of spins per second.
2. **`SentinelWorker` (Integrity Police):** Runs every 10 minutes. It sums up every row in the `Transactions` table for a user (`SUM(Amount)`) and compares it to their `User.Balance`. If they differ by even a cent, it triggers a "Critical Anomaly" alert.
3. **`TournamentPayoutWorker`:** Wakes up on the 1st of every month. It calculates the ROI (`((TotalPaid - TotalWagered) / TotalWagered) * 100`) for all players, ranks the Top 10, calculates dynamic prize pools (+1% of platform wagers), and automatically credits their wallets.

---

## 📚 Documentation & Deep Dive

For a comprehensive understanding of the mathematics, game rules, and structural design, refer to the detailed design documents in the `GameDesign/` folder:

* **Architecture & Flow:**
  * 🏗️ [The Trinity Architecture](GameDesign/Core_Logic/The_Trinity_Architecture.md)
  * 🔒 [Security Specifications](GameDesign/Core_Logic/Security_Specifications.md)
* **Economy & Mathematics:**
  * 🧠 [The Brain Intelligence](GameDesign/Core_Logic/The_Brain_Intelligence.md)
  * 🏦 [Financial Vault & RTP](GameDesign/Core_Logic/Financial_Vault_RTP.md)
* **Engines & Optimization:**
  * ⚡ [Optimization & Scripting](GameDesign/Core_Logic/OptimizationAndScripting.md)
  * ⚙️ [Automation Specs](GameDesign/Core_Logic/Automation_Specs.md)
* **Specific Games:**
  * 🎰 [CloverChase Logic](GameDesign/Games/CloverChase.md) & [Rules](GameDesign/Games/CloverChase_Rules_Updated.md)

---

## 📂 Project Structure Map

```text
AleaSim/
├── AleaSim.Api/          # Controllers, SignalR Hubs, Background Workers, JWT Auth
├── AleaSim.Client/       # Blazor WebAssembly, MudBlazor/Bootstrap UI, JS Game Renderers
├── AleaSim.Domain/       # The Core: BrainService, VaultService, Game Engines, Entities
├── AleaSim.Persistence/  # EF Core DbContext, Migrations, Dapper queries
├── AleaSim.Shared/       # DTOs and Enums shared between API and Client
├── AleaSim.CLI/          # Administrative Console Tool (Simulation Center)
├── AleaSim.Tests/        # xUnit Tests for Vault Integrity, RNG Distribution
└── GameDesign/           # 📚 High-level markdown architecture and math docs

```

---

## 🚀 Setup & Installation Guide

The platform is fully containerized and cloud-ready, supporting Docker Compose for rapid deployment.

### 1. Prerequisites

* **.NET 8.0 SDK**
* **MySQL / MariaDB** (or SQL Server)
* **Redis** (Mandatory for SignalR Backplane, Locks, and Caching)

### 2. Configuration (`appsettings.Development.json`)

Configure these keys in the `AleaSim.Api` project:

| Key | Description |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | `Server=localhost;Database=aleasim;Uid=root;Pwd=...` |
| `ConnectionStrings:Redis` | `localhost:6379,password=...` |
| `Jwt:Key` | 256-bit Security Key for signing tokens |
| `Admin:Id` | GUID for the Root Admin account |

### 3. Database Initialization

Run EF Core Migrations to build the schema:

```bash
dotnet ef database update --project AleaSim.Persistence --startup-project AleaSim.Api

```

### 4. Running the Platform Locally

Open two separate terminals to start the ecosystem:

```bash
# Terminal 1: Start Backend API & Workers
cd AleaSim.Api
dotnet run

# Terminal 2: Start Frontend Client (Blazor WASM)
cd AleaSim.Client
dotnet run

```

Navigate to `https://localhost:5001` (or the respective port in your console) to launch the simulation.

---

*© 2026 AleaSim Entertainment - Provably Fair, AI-Driven, Scalable.*
