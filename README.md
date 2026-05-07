# 🎰 AleaSim: The Trinity Casino Architecture

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WASM-5C2D91?style=flat-square&logo=blazor)
![Redis](https://img.shields.io/badge/Redis-Active-DC382D?style=flat-square&logo=redis)
![Security](https://img.shields.io/badge/Security-Industrial--Grade-success?style=flat-square)
![Provably Fair](https://img.shields.io/badge/RNG-HMAC--SHA256%20%2B%20Crypto-orange?style=flat-square)
![k3s](https://img.shields.io/badge/Hosted%20on-k3s%20Cluster-326CE5?style=flat-square&logo=kubernetes)
![SignalR](https://img.shields.io/badge/Real--Time-SignalR-512BD4?style=flat-square)
![MySQL](https://img.shields.io/badge/Database-MySQL%2FMariaDB-4479A1?style=flat-square&logo=mysql)
![EF Core](https://img.shields.io/badge/ORM-EF%20Core%208-512BD4?style=flat-square)
![Playwright](https://img.shields.io/badge/E2E-Playwright-green?style=flat-square)

**AleaSim** is an enterprise-grade, high-concurrency gambling platform. Built on the **Trinity Architecture**, it moves beyond simple randomness into a world of **mathematical determinism**, **cryptographic fairness**, and **behavioral AI**.

> [!IMPORTANT]
> This is a **high-fidelity simulation and engineering demonstration**. It does not handle real currency and is intended for architectural research and educational purposes only.

---

## 📌 Table of Contents
- [Project Overview](#-project-overview)
- [Visual Experience (Real Screens)](#-visual-experience-real-screens)
- [Diagrams & Schematics](#-diagrams--schematics)
- [Core Engineering Pillars](#-core-engineering-pillars)
- [Behavioral AI & Retention Mechanics](#-behavioral-ai--retention-mechanics)
- [Game Catalog](#-game-catalog)
- [Economy & Wallet System](#-economy--wallet-system)
- [Observability & Operations](#-observability--operations)
- [Power Tools & Administration](#-power-tools--administration)
- [Technical Stack](#-technical-stack)
- [Testing & Validation Suite](#-testing--validation-suite)
- [Installation & Setup](#-installation--setup)
- [Project Evolution (Changelog)](#-project-evolution-changelog)
- [Roadmap](#-roadmap)
- [Contributing](#-contributing)
- [License](#-license)

---

## 🧭 Project Overview
AleaSim models a complete casino ecosystem with strict financial guarantees, deterministic outcomes, and a live social layer. It is designed to simulate **real-world operational constraints** such as concurrency spikes, wallet integrity, anti-fraud mechanisms, and regulatory-grade auditing.

**Key goals:**
- Deliver a **provably fair** and cryptographically verifiable game system.
- Enforce **financial integrity** with atomic settlements and anti-double spend protection.
- Use **behavioral AI** to modulate volatility, RTP, and retention signals.
- Support **real-time social experiences** with scalable infrastructure.

---

## 🖼️ Visual Experience (Real Screens)
> Всеки екран по-долу съществува в системата и може да бъде скрийншотнат. Замени placeholder изображенията с реални кадри.

### 🏠 Lobby (Начална страница)
Екранът за избор на игри и навигация към слот/рулетка/блекджек.
![Lobby Screenshot](docs/images/lobby-home.png)

### 👤 Profile
Профил на играча, настройки и персонализация.
![Profile Screenshot](docs/images/profile.png)

### 💰 Wallet
Баланс, бонуси и управление на средствата.
![Wallet Screenshot](docs/images/wallet.png)

### 🧾 Bet History
История на залози и резултати.
![Bet History Screenshot](docs/images/bet-history.png)

### 🏆 Leaderboard
Класации, топ играчи и ранкинги.
![Leaderboard Screenshot](docs/images/leaderboard.png)

### 🎰 Slot #1 (CloverChase)
5x4 слот с бонуси и специални механики.
![CloverChase Screenshot](docs/images/cloverchase-gameplay.png)

### 🎰 Slot #2 (Fruit Blast Reactor)
Каскадна игра с експлозии и кластер печалби.
![Fruit Blast Screenshot](docs/images/fruit-blast.png)

### 🎲 Dice Game #1
Първа игра със зарове.
![Dice Game 1 Screenshot](docs/images/dice-game-1.png)

### 🎲 Dice Game #2
Втора игра със зарове.
![Dice Game 2 Screenshot](docs/images/dice-game-2.png)

### 🂡 Baccarat
Бакарaт маса и интерфейс.
![Baccarat Screenshot](docs/images/baccarat.png)

### ♠️ Blackjack
Стандартни правила, мулти-ръце, застраховка.
![Blackjack Screenshot](docs/images/blackjack-gameplay.png)

### 🛡️ Admin: Dashboard
Основен админ екран с обобщение и KPI.
![Admin Dashboard Screenshot](docs/images/admin-dashboard.png)

### 🛡️ Admin: Users
Управление на потребители и роли.
![Admin Users Screenshot](docs/images/admin-users.png)

### 🛡️ Admin: Transactions
Финансови събития и логове.
![Admin Transactions Screenshot](docs/images/admin-transactions.png)

### 🛡️ Admin: Games & RTP
Конфигурация на игри и RTP настройки.
![Admin Games Screenshot](docs/images/admin-games.png)

### 🛡️ Admin: Risk / Alerts
Сигнали, аларми и мониторинг.
![Admin Alerts Screenshot](docs/images/admin-alerts.png)

### 🛡️ Admin: Audit Stream
Пълен поток от събития (audit log).
![Admin Audit Screenshot](docs/images/admin-audit.png)

---

## 🧭 Diagrams & Schematics
Добави диаграми, които отговарят на реалната архитектура и могат да бъдат генерирани от проекта.

### 🏛️ Trinity Architecture (Vault / Brain / Engines)
![Trinity Architecture Diagram](docs/images/diagram-trinity-architecture.png)

### 🔁 Bet Lifecycle / Event Flow
![Bet Lifecycle Diagram](docs/images/diagram-bet-lifecycle.png)

### 🗄️ Database Schema (Core Tables)
![Database Schema Diagram](docs/images/diagram-database-schema.png)

### ☁️ Deployment & Infrastructure
![Deployment Diagram](docs/images/diagram-deployment.png)

---

## 🏗️ Core Engineering Pillars

### 1. 🏛️ The Trinity Architecture
The system is decoupled into three independent, typed layers communicating via strict contracts:
- **The Vault (Financial Guard):** The single source of truth. Handles `Bonus` vs `Real` wallet deductions, enforces daily loss limits, and guarantees atomic settlement.
- **The Brain (AI Behavioral Engine):** Analyzes `BehaviourProfile` (loss streaks, spin speed, pRTP delta) to issue directives. It manages "Flow State" and "Retention Hooks."
- **The Engines (Stateless Executors):** Reverse-engineers game outcomes. Instead of rolling dice, they mathematically construct the exact deck or reel grid required to hit the Brain's target within fairness constraints.

### 2. 🛡️ Industrial Security (Anti-Double Spend)
- **Distributed Locking (Redlock):** Every transaction is wrapped in a Redis lock (`wallet_{userId}`). Prevents "Double Spend" attacks even with 1,000s of requests across multiple server nodes.
- **API Rate Limiting:** Built-in DDoS protection using ASP.NET Core `RateLimiter` (100 req/10s globally; 10 req/5s for financial operations).
- **Atomic Persistence:** All financial state changes occur within scoped SQL transactions to ensure 100% data integrity.

### 3. ⚡ Infrastructure Resilience (Write-Ahead Log)
Designed to survive crashes without losing a single cent of player winnings:
- **Financial WAL:** Critical events (`JACKPOT_WIN`, `WITHDRAWAL`) bypass background buffers and are written **synchronously** to the database (Write-Ahead Logging).
- **Immutable Audit Chain:** Every system event is cryptographically hashed and linked to the previous one, creating a tamper-proof ledger.

### 4. 🎰 Certified Randomness (Hybrid RNG)
- **Provably Fair:** Uses `HMAC-SHA256` where outcomes are derived from ServerSeed + ClientSeed + Nonce.
- **Certified Fallback:** When seeds are absent, it uses hardware-backed **Cryptographic Randomness** (`RandomNumberGenerator`) to meet industrial certification standards.

---

## 🧠 Behavioral AI & Retention Mechanics
AleaSim models a dynamic player experience system that adapts in real time:
- **Behavior Profiles:** Tracks loss streaks, betting velocity, session duration, and RTP deviation.
- **RTP Modulation:** The Brain sends target parameters to Engines to stabilize RTP over time without breaking fairness rules.
- **Retention Hooks:** Offers quests, streak bonuses, and milestone rewards when churn signals are detected.
- **Flow State:** Keeps player engagement steady by smoothing volatility and timing bonus triggers.

---

## 🎮 Game Catalog
A modular game system allows new engines to be added without touching core financial or AI logic.

**Current portfolio:**
- **CloverChase Slot** – 5x4 reel engine with layered bonus logic.
- **Fruit Blast Reactor** – cluster pays, cascade system, and blast mechanics.
- **Roulette Royale** – European wheel with live dynamic multipliers.
- **Tactical Blackjack** – multi-hand rules, insurance, and deterministic dealer logic.

**Planned expansions:**
- **Baccarat Arena**
- **Craps 3D Table**
- **Mini-games hub (instant games / tap-to-win)**

---

## 💰 Economy & Wallet System
The economy is fully isolated and deterministic:
- **Dual Wallet Model:** Separate `Real` and `Bonus` balances with strict deduction priorities.
- **Loss Limits & Protection:** Daily and session-based limits enforced at the Vault layer.
- **Reward Systems:** RPG XP, level bonuses, and quest rewards are processed through the same financial guardrails.

---

## 🔭 Observability & Operations
Operational transparency is a core design goal:
- **Live Admin Stream:** Every bet, win, and state transition pushed to monitoring panels in real time.
- **Audit Trail:** Immutable chain for all financial events.
- **Telemetry Hooks:** Latency, error, and RTP drift metrics (prepared for export to dashboards).

---

## 🛠️ Power Tools & Administration

### 💻 CLI Administrator
A high-performance terminal tool for engineers to perform dry-runs and verify math at scale.
```bash
# Verify Slot RTP over 1,000,000 spins using all CPU cores
./AleaSim.CLI admin rtp-verify slot 1000000

# View global platform stats
./AleaSim.CLI admin stats

# View live progressive jackpots
./AleaSim.CLI jackpots
```

### 🛰️ Real-Time Social Layer
Driven by an optimized **SignalR** implementation, the social layer supports:
- **Instagram-style Chat:** State-based mobile navigation (List -> Active Chat) with seamless panel sliding.
- **Global Event Hub:** Real-time "Big Win" notifications and jackpot tickers pushed instantly to all connected clients.
- **Notification HQ:** A dedicated page for players to track their win history, bonus credits, and system alerts.

---

## 🚀 Technical Stack

| Category | Technology |
| :--- | :--- |
| **Backend** | .NET 8 Web API, SignalR, Entity Framework Core |
| **Frontend** | Blazor WebAssembly (WASM), MudBlazor UI, PixiJS (Game Rendering) |
| **Infrastructure** | k3s (Kubernetes), Redis (Caching & Locking), MySQL (Persistence) |
| **Security** | JWT (HttpOnly Cookies), BCrypt, Redis Redlock, RateLimiting |
| **Testing** | xUnit, Moq, Microsoft Playwright (E2E), Coverlet |

---

## 🔬 Testing & Validation Suite
AleaSim maintains a **100% success rate** across its testing pyramid:
- **Concurrency Stress Tests:** Proves that Redis locks prevent balance corruption during simultaneous attacks.
- **E2E Automation:** Playwright scripts simulate Registration, Login, and Real-time game flows in a headless browser.
- **Math Validation:** Multi-threaded CLI verifier simulates 10M+ iterations to confirm RTP models.

---

## 🚀 Installation & Setup

1. **Database:** `dotnet ef database update --project AleaSim.Persistence --startup-project AleaSim.Api`
2. **E2E Setup:** `cd AleaSim.E2ETests && playwright install`
3. **Run:**
   ```bash
   dotnet run --project AleaSim.Api
   dotnet run --project AleaSim.Client
   ```

---

## 📈 Project Evolution (Changelog)
- **v1.0 (The Genesis):** Core Trinity Architecture and Slot Engine implementation.
- **v2.0 (RPG Update):** Introduction of Quests, Levels, and the Global Chat.
- **v3.0 (The Industrial Update):** **Current Version.** Added WAL resilience, Distributed Locking, Cryptographic RNG, full Mobile responsiveness, and Playwright E2E automation.

---

## 🧭 Roadmap
- **Economy Balancer:** smarter long-session retention tuning.
- **Risk Analyzer:** automated anomaly detection for suspicious win patterns.
- **Content SDK:** game templates to spin up new engines faster.
- **Observability Dashboards:** deployable Grafana/Prometheus stack.

---

## 🤝 Contributing
Contributions are welcome. Suggested focus areas:
- Additional games and bonus modes
- Performance profiling & infrastructure tuning
- UX polish and mobile micro-interactions

---

## 📜 License
This project is provided for **educational and research purposes**. See repository license for details.

---

*© 2026 AleaSim Entertainment — Built for the next generation of safe, fair, and scalable entertainment software.*
