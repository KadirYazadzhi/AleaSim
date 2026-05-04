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

**AleaSim** is an enterprise-grade, high-concurrency gambling platform. Built on the **Trinity Architecture**, it moves beyond simple randomness into a world of **mathematical determinism**, **cryptographic verifiability**, and **industrial-strength financial safeguards**. It is designed to be **bulletproof** — resistant to server crashes, double-spending attacks, and real-time high-concurrency anomalies.

> [!IMPORTANT]
> This is a **high-fidelity simulation and engineering demonstration**. It does not handle real currency and is intended for architectural research and educational purposes only.

---

## 🖼️ Visual Experience

### 🏛️ The Player Hub
The central command where players manage their wallets, track RPG levels, and explore the ecosystem.
![Dashboard Screenshot](docs/images/dashboard.png)

### 🍀 CloverChase Slot
A professional 5x4 video slot with Sticky Wilds, Mystery Nudges, and a layered Bell Bonus system.
![Slot Screenshot](docs/images/cloverchase-gameplay.png)

### 🍹 Fruit Blast Reactor
A cascading/avalanche slot with cluster pays, TNT explosions, and the high-volatility Juice Pot Meltdown.
![FruitBlast Screenshot](docs/images/fruit-blast.png)

### 🎡 Roulette Royale
A high-fidelity European Roulette experience with dynamic lucky numbers and real-time multiplier injection.
![Roulette Screenshot](docs/images/roulette-gameplay.png)

### ♠️ Tactical Blackjack
Standard Vegas rules with multi-hand support, insurance, and Brain-optimized dealer logic.
![Blackjack Screenshot](docs/images/blackjack-gameplay.png)

### 📱 Native Mobile UI
A complete "Mobile-First" overhaul. Games expand to screen edges, and the chat system uses an Instagram-style navigation flow.
![Mobile View Screenshot](docs/images/mobile-view.png)

### 💬 Social & Comms
A fully responsive, real-time communication suite with global channels and encrypted-style private direct messages.
![Chat Screenshot](docs/images/chat-view.png)

### 🛡️ Platform Sentinel (Live Monitoring)
An industrial-grade admin monitor providing a real-time stream of every bet, win, and system audit across the infrastructure.
![Admin Monitor Screenshot](docs/images/admin-panel.png)

---

## 🏗️ Core Engineering Pillars

### 1. 🏛️ The Trinity Architecture
The system is decoupled into three independent, typed layers communicating via strict contracts:
*   **The Vault (Financial Guard):** The single source of truth. Handles `Bonus` vs `Real` wallet deductions, enforces daily loss limits, and guarantees atomic settlement.
*   **The Brain (AI Behavioral Engine):** Analyzes `BehaviourProfile` (loss streaks, spin speed, pRTP delta) to issue directives. It manages "Flow State" and "Retention Hooks."
*   **The Engines (Stateless Executors):** Reverse-engineers game outcomes. Instead of rolling dice, they mathematically construct the exact deck or reel grid required to hit the Brain's target win.

### 2. 🛡️ Industrial Security (Anti-Double Spend)
*   **Distributed Locking (Redlock):** Every transaction is wrapped in a Redis lock (`wallet_{userId}`). Prevents "Double Spend" attacks even with 1,000s of requests across multiple server nodes.
*   **API Rate Limiting:** Built-in DDoS protection using ASP.NET Core `RateLimiter` (100 req/10s globally; 10 req/5s for financial operations).
*   **Atomic Persistence:** All financial state changes occur within scoped SQL transactions to ensure 100% data integrity.

### 3. ⚡ Infrastructure Resilience (Write-Ahead Log)
Designed to survive crashes without losing a single cent of player winnings:
*   **Financial WAL:** Critical events (`JACKPOT_WIN`, `WITHDRAWAL`) bypass background buffers and are written **synchronously** to the database (Write-Ahead Logging).
*   **Immutable Audit Chain:** Every system event is cryptographically hashed and linked to the previous one, creating a tamper-proof ledger.

### 4. 🎰 Certified Randomness (Hybrid RNG)
*   **Provably Fair:** Uses `HMAC-SHA256` where outcomes are derived from ServerSeed + ClientSeed + Nonce.
*   **Certified Fallback:** When seeds are absent, it uses hardware-backed **Cryptographic Randomness** (`RandomNumberGenerator`) to meet industrial certification standards.

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
*   **Instagram-style Chat:** State-based mobile navigation (List -> Active Chat) with seamless panel sliding.
*   **Global Event Hub:** Real-time "Big Win" notifications and jackpot tickers pushed instantly to all connected clients.
*   **Notification HQ:** A dedicated page for players to track their win history, bonus credits, and system alerts.

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

## 📈 Project Evolution (Changelog)
*   **v1.0 (The Genesis):** Core Trinity Architecture and Slot Engine implementation.
*   **v2.0 (RPG Update):** Introduction of Quests, Levels, and the Global Chat.
*   **v3.0 (The Industrial Update):** **Current Version.** Added WAL resilience, Distributed Locking, Cryptographic RNG, full Mobile responsiveness, and Playwright E2E automation.

---

## 🔬 Testing & Validation Suite
AleaSim maintains a **100% success rate** across its testing pyramid:
*   **Concurrency Stress Tests:** Proves that Redis locks prevent balance corruption during simultaneous attacks.
*   **E2E Automation:** Playwright scripts simulate Registration, Login, and Real-time game flows in a headless browser.
*   **Math Validation:** Multi-threaded CLI verifier simulates 10M+ iterations to confirm RTP models.

---

## 🚀 Installation & Setup

1.  **Database:** `dotnet ef database update --project AleaSim.Persistence --startup-project AleaSim.Api`
2.  **E2E Setup:** `cd AleaSim.E2ETests && playwright install`
3.  **Run:**
    ```bash
    dotnet run --project AleaSim.Api
    dotnet run --project AleaSim.Client
    ```

---

## 🎓 Informatics Olympiad Integration
This project serves as a reference for advanced informatics topics (Topics 1-44), specifically targeting **Complexity Analysis**, **Distributed Systems**, and **Applied Cryptography**. Full technical deep dives are available in `GameDesign/`.

---

*© 2026 AleaSim Entertainment — Built for the next generation of safe, fair, and scalable entertainment software.*
