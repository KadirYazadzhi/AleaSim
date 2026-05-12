# 🎰 AleaSim: The Trinity Casino Architecture

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WASM-5C2D91?style=flat-square&logo=blazor)
![Redis](https://img.shields.io/badge/Redis-Active-DC382D?style=flat-square&logo=redis)
![Security](https://img.shields.io/badge/Security-Industrial--Grade-success?style=flat-square)
![Provably Fair](https://img.shields.io/badge/RNG-HMAC--SHA256%20%2B%20Crypto-orange?style=flat-square)
![SignalR](https://img.shields.io/badge/Real--Time-SignalR-512BD4?style=flat-square)
![MySQL](https://img.shields.io/badge/Database-MySQL-4479A1?style=flat-square&logo=mysql)

**AleaSim** is a high-fidelity, enterprise-grade simulation of a modern gambling ecosystem. Built on the **Trinity Architecture**, it serves as a masterclass in building high-concurrency, mathematically deterministic, and cryptographically secure distributed systems.

![AleaSim Banner](Images/banner.png)

---

## 🎯 Executive Vision & System Purpose
AleaSim was engineered to bridge the gap between **high-stakes financial reliability** and **immersive psychological engagement**. The platform isn't just a collection of games; it's a living laboratory for behavioral AI, autonomous economy balancing, and industrial-grade transaction safeguarding.

### 🏠 Landing & Navigation Experience
The system provides two distinct entry points tailored to the user's authentication state, ensuring a high-conversion registration funnel and a socially-active member area.

**Guest Experience (Non-Registered):**
![AleaSim Home Page](Images/home-page.png)
*Figure 1: Public Landing Page—Showcasing the platform portfolio, hot games, and registration entry points.*

**Member Experience (Authenticated Lobby):**
![Main Lobby](Images/lobby.png)
*Figure 2: Registered User Lobby—The central dashboard for game navigation, live winner feeds, and social interaction.*

### Core Strategic Pillars:
1.  **Trustless Fairness:** Every spin is a cryptographic proof. The house cannot cheat because the outcome is pre-determined by a seed-pair that the player can verify.
2.  **Financial Resilience:** Using the **Vault Sentinel** logic, the platform operates with the same integrity as a high-frequency trading desk.
3.  **Behavioral Flow:** The system uses "The Brain" (AI) to eliminate the "dry streaks" that kill user retention, smoothing out the variance without breaking the long-term RTP.

---

## 🏛️ 1. The Trinity Architecture: A Deep Dive
The system is decoupled into three distinct layers, each with a specialized responsibility. This separation of concerns allows for horizontal scaling and prevents "Leaky Abstractions" where game logic could impact financial data.

### 🏦 1.1 The Vault (Financial Guard & Ledger)
The Vault is the only layer authorized to touch the `Wallet` and `Transaction` tables.
- **Atomic Concurrency (Redis Redlock):** To prevent "Race Conditions" where a player might click "Spin" twice on two different devices simultaneously, the Vault locks the user's ID globally for the duration of the transaction.
- **Smart Solvency Protection:** Every win is pre-calculated against the `CasinoPool`. If a win would bankrupt a specific pool, the system dynamically reroutes the outcome or caps it, ensuring the platform remains solvent 24/7.
- **Idempotent Operations:** Every financial request carries a unique ID. If a request is re-sent due to a network glitch, the Vault identifies the ID and returns the previous result instead of deducting balance again.
- **Dual Wallet Prioritization:** Bonus funds are automatically consumed before real cash, with real-time wagering requirement tracking.

![Wallet and Financial Economy](Images/User/wallet.png)
*Figure 3: The Player Wallet—Advanced financial management with segregated balance tracking.*

---

### 🧠 1.2 The Brain (Behavioral Intelligence Engine)
The Brain is the director of the experience. It doesn't decide *if* you win (that's the math), but it decides *when* to offer engagement features.
- **Retention Hooks:** Analyzes `LossStreakCount`. If a player is losing too fast, it injects a "Saver Bomb" or a "Near Miss" to trigger dopamine release and prevent churn.
- **Flow State Management:** Monitors click-speed. If a player is playing fast, it reduces animation times and increases the "Action Frequency" to maintain the immersive loop.
- **Weighted Directives:** Generates `BrainDirectives` stored in Redis. These tell the game engines to prefer certain visual patterns (like a 4-of-a-kind win) to hit the desired "Player Satisfaction" score.

![Live Event & Brain Monitoring](Images/Admin/admin-live-monitor.png)
*Figure 4: Live Event Monitor—observing the Brain's real-time directives and outcomes.*

---

### ⚙️ 1.3 The Engines (Stateless Game Logic)
Engines are purely mathematical executors. They receive a directive and a seed, and they return a result.
- **Reconstruction Algorithm:** All active features (Respins, Sticky Wilds) are saved in Redis. If a player closes their tab, the Engine can "re-play" the state exactly as it was, providing a seamless "Resume" feature.
- **Deterministic Outcomes:** Given the same ServerSeed, ClientSeed, and Nonce, the Engine will *always* produce the identical grid. This is the foundation of our **Provably Fair** system.

---

## 🗄️ 2. Data Strategy & Persistence
AleaSim utilizes a hybrid storage model to achieve **sub-10ms response times** while maintaining **100% data durability**.

### 💿 2.1 MySQL/MariaDB (The Definitive Ledger)
- **Financial WAL (Write-Ahead Log):** Every credit/debit is recorded in a transactional ledger before the balance is updated.
- **Immutable Audit Trail:** All administrative actions (RTP changes, manual credits) are logged with a cryptographic hash of the previous log entry.
- **Relational Integrity:** Links every spin to a `GameRound` ID, a `Session` ID, and a `VaultTransaction` ID.

The following diagram illustrates the enterprise-grade complexity of the 25+ core entities and their interconnected relationships.

```mermaid
erDiagram
    USER ||--o{ TRANSACTION : "records flow"
    USER ||--o| PLAYER_PROFILE : "owns"
    USER ||--o{ GAME_SESSION : "plays"
    USER ||--o{ USER_QUEST : "progresses"
    USER ||--o{ USER_ACHIEVEMENT : "earns"
    USER ||--o{ USER_VOUCHER : "redeems"
    USER ||--o{ TOURNAMENT_ENTRY : "enters"
    USER ||--o{ CHAT_MESSAGE : "sends"
    
    GAME_SESSION ||--o{ BET : "contains"
    BET ||--o| GAME_ROUND : "resolves to"
    GAME_ROUND ||--o| OUTCOME : "produces"
    
    QUEST ||--o{ USER_QUEST : "defines"
    ACHIEVEMENT ||--o{ USER_ACHIEVEMENT : "defines"
    VOUCHER ||--o{ USER_VOUCHER : "defines"
    
    TOURNAMENT ||--o{ TOURNAMENT_ENTRY : "hosts"
    TOURNAMENT ||--o{ TOURNAMENT_WINNER : "crowns"
    
    GAME ||--o{ GAME_SESSION : "instantiates"
    GAME ||--o{ JACKPOT : "feeds"
    GAME ||--o{ RTP_STATISTICS : "tracks"
    
    SYSTEM_ERROR }o--|| GAME_ROUND : "logs"
    AUDIT_EVENT }o--|| USER : "monitors"
```
*Figure 5: Enterprise Entity-Relationship Diagram—Mapping the 100% relational integrity of the AleaSim ecosystem.*

![Detailed Bet History](Images/User/bet-history.png)
*Figure 6: Historical Ledger—viewing the settled results from the persistent storage.*

---

### ⚡ 2.2 Redis (The Real-Time State Hub)
- **High-Velocity Locking:** Manages the `Redlock` distributed locks for all financial operations.
- **Social Backplane:** Powers the real-time chat and global winner notifications via SignalR.
- **Caching Layer:** Stores the "Active Board" for slots, allowing for instant page refreshes without hitting the main database.
- **State Persistence:** Mid-game states (Respins, Bonus phases) are written to Redis with a TTL of 30 minutes to support stateless API horizontal scaling.

---

## 🎮 3. Game Portfolio: Mathematical Specs

### 🍀 3.1 Clover Chase (The Strategic Slot)
![Clover Chase Gameplay](Images/Games/clover-chase.png)
*Figure 7: Clover Chase—the sticky respin feature in action.*

- **Paid Respin Mechanic:** Unlike "Free Spins," these are **Paid Respins**. Every spin costs a bet, but the **Sticky Wild Clovers (ID 8)** make a win almost guaranteed. This creates a high-turnover, high-reward loop that players love.
- **Bell Bonus (Hold & Win):** Collecting 5+ Clovers flips them into Bells.
    - **Multiplier Range:** 2x to 100x per bell.
    - **Jackpots:** Mini (1000x Denom) and Minor (5000x Denom) are fixed based on the denomination choice, incentivizing high-denom play.

---

### ☢️ 3.2 Fruit Blast Reactor (Cascading Chaos)
![Fruit Blast Reactor](Images/Games/fruir-blast.png)
*Figure 8: Fruit Blast—cascading explosions filling the Juice Meter.*

- **Recursive Avalanche:** A server-side algorithm that calculates win-cascades until no clusters remain.
- **Juice Meter Progression:**
    - **50 Points:** 2x Multiplier.
    - **100 Points:** 5x Multiplier.
    - **150 Points:** 10x Multiplier.
    - **200 Points (Meltdown):** **18x Multiplier** + award the total **Juice Reservoir Jackpot**.
- **Lifetime "Vitamin" Progression:** Every 5,000 exploded fruits awards a **3x3 Mega Golden Apple** (guaranteed massive win).

---

### 🎲 3.3 Table & Original Games

| 🎡 Roulette Royale | ♠️ Multi-Hand Blackjack |
| :---: | :---: |
| ![Roulette](Images/Games/roulette.png) | ![Blackjack](Images/Games/blackjack.png) |

| 🎲 Neon Dice | 🎲 Crazy Dice |
| :---: | :---: |
| ![Neon Dice](Images/Games/neon-dice.png) | ![Crazy Dice](Images/Games/crazy-dice.png) |

---

## 🛡️ 4. Security & Compliance Framework
![Provably Fair Verification Page](Images/User/fairness.png)
*Figure 9: Transparency Dashboard—where players can verify the cryptographic integrity of their spins.*

- **HMAC-SHA256 Fairness:** The "Server Seed" is hashed and shown to the player *before* they play. They provide the "Client Seed." The result is the hash of both. This proves the house didn't change the result after seeing the bet.
- **Rate Limiting Middleware:** Prevents brute-force attacks and API abuse with a sliding window algorithm.
- **JWT HttpOnly Security:** Authentication tokens are stored in HttpOnly cookies to prevent XSS-based session theft.
- **Anti-Double Spend:** The Vault layer rejects any transaction that would result in a negative balance, even if requested simultaneously.

---

## 🛰️ 5. Real-Time Social & Social Engineering
AleaSim is designed to be "alive."
- **Global Winner Feed:** Every "Big Win" (over 10x bet) is broadcast via SignalR to every connected user.
- **Must-Drop Jackpots:** Progressive pools that are programmed to trigger before a specific dollar amount, creating "FOMO" (Fear Of Missing Out) and increasing spin-frequency.
- **Instagram-Style Chat:** A fully responsive, state-based chat system that feels native on mobile and desktop.

---

## 🛡️ 6. Administrative Command Suite (The Ops View)
The Admin Panel is a full-scale NOC (Network Operations Center) for the platform.

### 📊 6.1 Global Dashboard & Simulation
The simulation center allows admins to perform dry-runs of millions of spins to verify the mathematical stability of the platform.
![Main Simulation Dashboard](Images/Admin/admin-simulation-center.png)
*Figure 10: Simulation Center—stress-testing the game engines at industrial scale.*

![Admin Dashboard](Images/Admin/admin-dashboard.png)
*Figure 11: Global KPIs and real-time platform health monitoring.*

---

### 👥 6.2 Player Manager & Economics
Detailed control over user accounts, balances, and historical behavior.

![Player Manager](Images/Admin/admin-player-manager.png)
*Figure 12: Player Management—Deep-dive into individual user activity and financial status.*

---

### ⚙️ 6.3 Configuration & Promo System
Real-time control over game parameters, tournament rules, and voucher distribution.

**Platform Configuration:**
![Settings](Images/Admin/admin-settings.png)

**Voucher & Promo Management:**
![Vouchers](Images/Admin/admin-voucher.png)

**Live Audit & Monitoring:**
![Audit](Images/Admin/admin-live-monitor.png)

**Tournament Operations:**
![Tournaments](Images/Admin/admin-tournaments.png)

---

## 👤 7. User Experience & Personalization
Players have access to a rich set of tools to manage their experience, from detailed profiles to real-time social interaction.

![Player Profile](Images/User/profile.png)
*Figure 13: Player Profile—managing achievements and personal settings.*

![Global Leaderboard](Images/User/leaderboard.png)
*Figure 14: Competitive Ecosystem—tracking top ROI players in real-time.*

---

## 🚀 8. Technical Implementation & Scale
### Stack Overview:
- **Backend:** .NET 8 (C#), Entity Framework Core 8, SignalR (WebSockets).
- **Frontend:** Blazor WebAssembly (WASM), MudBlazor UI, PixiJS (Game Rendering).
- **Caching/State:** Redis (Redlock, SignalR Backplane).
- **Database:** MySQL 8.0 / MariaDB.
- **CI/CD & Hosting:** k3s (Kubernetes) Cluster with Dockerized Microservices.

### Rapid Setup:
1.  **Clone & Migrations:**
    ```bash
    dotnet ef database update --project AleaSim.Persistence --startup-project AleaSim.Api
    ```
2.  **RNG Initialization:** Seeds are automatically generated on first run.
3.  **Run:**
    ```bash
    # API & Engines
    dotnet run --project AleaSim.Api
    
    # Blazor Client
    dotnet run --project AleaSim.Client
    ```

---

## 📈 9. Future Roadmap: The AI Frontier
1.  **RiskSense AI:** Neural network integration for real-time detection of "Botting" and "Arbitrage" patterns.
2.  **Multi-Instance Sync:** Supporting 100,000+ concurrent players across globally distributed k8s nodes.
3.  **Content SDK:** Allowing 3rd party developers to build games using our Trinity Contracts.

---

*© 2026 AleaSim Entertainment. Architected for extreme performance, cryptographic security, and mathematical integrity.*
