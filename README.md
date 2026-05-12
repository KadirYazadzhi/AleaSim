# 🎰 AleaSim: The Trinity Casino Architecture

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WASM-5C2D91?style=flat-square&logo=blazor)
![Redis](https://img.shields.io/badge/Redis-Active-DC382D?style=flat-square&logo=redis)
![Security](https://img.shields.io/badge/Security-Industrial--Grade-success?style=flat-square)
![Provably Fair](https://img.shields.io/badge/RNG-HMAC--SHA256%20%2B%20Crypto-orange?style=flat-square)
![k3s](https://img.shields.io/badge/Hosted%20on-k3s%20Cluster-326CE5?style=flat-square&logo=kubernetes)
![SignalR](https://img.shields.io/badge/Real--Time-SignalR-512BD4?style=flat-square)
![MySQL](https://img.shields.io/badge/Database-MySQL-4479A1?style=flat-square&logo=mysql)

**AleaSim** is more than just a casino—it is a high-fidelity **financial and behavioral simulation ecosystem**. Built on the **Trinity Architecture**, it provides a blueprint for next-generation gambling platforms where security, mathematical transparency, and real-time engagement intersect.

![Simulation Center](Images/Admin/admin-simulation-center.png)

---

## 🎯 Project Vision & Core Goal
The primary objective of AleaSim is to solve the **"Trust & Scalability Gap"** in modern iGaming. Traditional platforms often struggle with transparent fairness and real-time performance under high load. 

![Lobby Home](Images/lobby.png)

**AleaSim addresses this by:**
1.  **Eliminating the "Black Box":** Providing a cryptographically verifiable (Provably Fair) trail for every single outcome.
2.  **Guaranteed Solvency:** Implementing the **Vault Layer** to ensure that the platform can never authorize a payout it cannot afford.
3.  **Behavioral Retention:** Using AI (The Brain) to create a "Flow State" that feels rewarding to the player while maintaining strict operator RTP targets.

---

## 🏛️ The Trinity Architecture: Deep Dive

### 🏦 1. The Vault (The Financial Sentinel)
The Vault acts as an immutable ledger and a real-time gatekeeper.
- **Idempotency Engine:** Every win claim is tied to a unique `ClaimId`. Even if a network error occurs and a request is retried, the Vault ensures the payout happens exactly once.
- **Anti-Fraud Guard:** Monitors for "High-Velocity Betting" and "Pattern Anomalies" at the API level, freezing suspicious transactions before they settle in the main ledger.
- **Liquidity Awareness:** The Vault doesn't just store balance; it knows the total pool of the casino. If a jackpot is triggered, it performs an instant verification to ensure the pool has not been drained by a concurrent win.

![Wallet and Economy](Images/User/wallet.png)

### 🧠 2. The Brain (Psychological Engineering)
The Brain is the "Director" of the player's journey.
- **Weighted RNG Influence:** Instead of flat probabilities, the Brain uses **dynamic weights**. If a player is on a 10-spin loss streak, the weights for "Small Wins" increase temporarily to maintain engagement (The Retention Hook).
- **Flow State Mapping:** By tracking the speed of clicks and bet size changes, the Brain identifies if a player is "In the Zone." It then adjusts the game's visual pacing and bonus frequency to maximize the duration of this state.

![Live Event Monitor](Images/Admin/admin-live-monitor.png)

### ⚙️ 3. The Engines (Stateless Math Executors)
Our engines are purely mathematical. They don't store player state; they receive a **Request Packet** and return a **Result Proof**.
- **Reconstruction Logic:** If a player refreshes their browser mid-respin, the Engine uses the data stored in the Redis cache to reconstruct the exact grid, ensuring the player sees no interruption in their experience.

---

## 🗄️ Persistence Layer: Database & State
AleaSim uses a hybrid persistence strategy to achieve both durability and extreme performance.

### 💿 MySQL (The Immutable Ledger)
- **Transactional History:** Stores every `Bet`, `Win`, and `Transaction` with high precision.
- **Audit Ledger:** A specialized table that tracks every administrative change (e.g., changing a game's RTP or adjusting a player's balance).
- **Player Profiles:** Stores XP, Levels, and Quest progress.

![Detailed Bet History](Images/User/bet-history.png)

### ⚡ Redis (The Real-Time Pulse)
- **Active Sessions:** Stores the current state of every active game round (e.g., which symbols are sticky on a slot).
- **Distributed Locking:** Uses **Redlock** to manage global concurrency for Jackpots and Wallets.
- **SignalR Backplane:** Ensures that real-time notifications (like a "Big Win") are synced across all server nodes in a cluster.

---

## 📊 Level of Completeness (Feature Matrix)

| Feature Category | Status | Details |
| :--- | :--- | :--- |
| **Financial (Vault)** | ✅ 100% | Atomic transactions, bonus/real balance, anti-double spend. |
| **AI (The Brain)** | 🟠 85% | Dynamic Directives and Retention Hooks active; shadow-mode tuning in progress. |
| **Slot Engine** | ✅ 100% | 5x4 Reels, Sticky Respins, Bonus Reveal, Jackpots. |
| **Reactor Engine** | ✅ 100% | Cascading wins, 4-level Juice Meter, Vitamin Overload. |
| **Real-Time Layer** | ✅ 95% | SignalR integration, Global Jackpots, Live Chat with mobile flow. |
| **Admin Panel** | 🔵 70% | Core management active; visual analytics & risk sense AI pending. |
| **Crypto Support** | ⏳ Planned | Native BTC/ETH on-chain monitoring. |

---

## 🛡️ Scalability & Concurrency Specifics
AleaSim is designed to scale horizontally across a **k3s (Kubernetes)** cluster.
- **Stateless API:** Since all critical state is in Redis/MySQL, any server node can handle any request at any time.
- **Throttling & Backpressure:** The system uses **Channel-based background workers** for non-critical tasks (like writing logs), ensuring the main game loop never waits for I/O.
- **Database Sharding Ready:** The schema is designed with `UserId` and `GameId` as primary partitions, allowing for easy database sharding as the player base grows.

---

## 🎮 Game Mechanics: The Logic Behind the Fun

### 🍀 Clover Chase (Slot)
- **Logic:** Uses a "Weighted Strip" approach. The strips are dynamically modified when a **Clover** lands to increase the weight of Wilds during the respin phase.
- **Mathematical Stabilizer:** The "Paid Respin" ensures that while the player wins often, the casino maintains a healthy edge through the spin cost.

![Clover Chase Gameplay](Images/Games/clover-chase.png)

### ☢️ Fruit Blast (Reactor)
- **Logic:** Uses a **Recursive Avalanche Algorithm**. Each win triggers a new "Drop" event that is calculated server-side and pushed to the client.
- **The Juice Pot:** This is a local jackpot specific to the game instance, creating a "Must-Win" FOMO effect as the meter approaches 200 points.

![Fruit Blast Reactor](Images/Games/fruir-blast.png)

---

## 🛡️ Administrative Command Center
The administration suite provides full transparency and control over the platform's economics and player base.

### 📊 Global Dashboard
![Admin Dashboard](Images/Admin/admin-dashboard.png)

### 👥 Player & User Management
![Player Manager](Images/Admin/admin-player-manager.png)

### ⚙️ Platform Configuration
![Platform Settings](Images/Admin/admin-settings.png)

---

## 👤 User Experience & Personalization
Players have access to a rich set of tools to manage their experience, from detailed profiles to real-time social interaction.

### 👤 Profile & Achievements
![Player Profile](Images/User/profile.png)

---

## 🧭 Future Evolution & Roadmap
1.  **RiskSense AI:** Implementation of a neural network to detect bot-like betting behavior.
2.  **Tournament 2.0:** Real-time competitive leaderboards with prize pools that grow based on participation.
3.  **Content SDK:** A set of tools for 3rd party developers to build and plug in their own Game Engines.
4.  **Full Observability:** Exporting all system metrics to Prometheus/Grafana for industrial-grade monitoring.

---

*© 2026 AleaSim Entertainment. Architected for extreme performance, security, and mathematical integrity.*
