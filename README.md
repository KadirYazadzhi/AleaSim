# 🎰 AleaSim: The Trinity Casino Architecture

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WASM-5C2D91?style=flat-square&logo=blazor)
![Redis](https://img.shields.io/badge/Redis-Active-DC382D?style=flat-square&logo=redis)
![Security](https://img.shields.io/badge/Security-Production--Ready-success?style=flat-square)
![Provably Fair](https://img.shields.io/badge/RNG-HMAC--SHA256-orange?style=flat-square)

AleaSim is an enterprise-grade gambling simulation platform built on the **Trinity Architecture**. It moves beyond simple random number generation to a behavioral engagement model where every outcome is mathematically deterministic, cryptographically verifiable, and AI-optimized.

---

## 🏗️ The Trinity Architecture
The system is divided into three strictly decoupled layers, ensuring maximum security and scalability.

1.  **🧠 THE BRAIN (Intelligence):** Analyzes player behavior (AvgSpinInterval, Loss Streaks, LTV) to determine the *optimal* outcome for retention. It doesn't roll dice; it makes business decisions. [Read More: The Brain Intelligence](GameDesign/Core_Logic/The_Brain_Intelligence.md)
2.  **⚙️ THE ENGINE (Visualization):** A "dumb" executor that receives a directive (e.g., "Target Win: $50") and reverse-engineers a visual result (Reel Stops, Card Sequence) that matches the target using Provably Fair logic. [Read More: CMS & Reverse Engineering](GameDesign/Core_Logic/CMS_Reverse_Engineering.md)
3.  **🏛️ THE VAULT (Finance):** The ultimate gatekeeper. It enforces the house edge via a global `PoolBalance` and per-user `Shadow Wallets`. If the pool cannot afford a win, the Vault denies it, forcing the Brain to recalculate. [Read More: Vault & RTP Control](GameDesign/Core_Logic/Financial_Vault_RTP.md)

---

## 🎮 Game Library
All 6 games feature **Auto-Resume** technology, allowing players to recover their exact state (Grid, Hands, Bonus Lives) after a disconnect or refresh.

| Game | Key Mechanics | Fairness Method |
| :--- | :--- | :--- |
| **🍀 Clover Chase** (Slot) | Hold & Win, Sticky Wild Respins, Mystery Nudges, Multi-tier Jackpots | Reel Stop Mapping |
| **♠️ Blackjack** | Multi-hand support, Split, Double Down, Insurance | Card Sequence |
| **🔴 Roulette Royale** | Classic European rules, Visual Ball Tracking, High-Limit tables | Single Number |
| **💎 Baccarat Royale** | Full "Third Card Rule" implementation, 8:1 Tie Payouts | Card Sequence |
| **🎲 Neon Dice** | Classic Slider (0-100) with dynamic multipliers | Percentage Roll |
| **💥 Crazy Dice** | Multi-dice mode (10 dice) with cumulative wins | Integer Sum |

---

## 🛡️ Security & Infrastructure
AleaSim is built for high-concurrency production environments.

*   **Cryptographic Trust:** Every round is generated via **HMAC-SHA256** using a hidden `ServerSeed`, a user-provided `ClientSeed`, and a unique `Nonce`. 
*   **Token Revocation (Token Zombie Fix):** Real-time session validation against Redis/SQL prevents usage of leaked or terminated tokens.
*   **Data Integrity:** A hashed **Audit Chain (Immutable Ledger)** tracks every system event. A dedicated background worker periodically verifies the ledger's integrity.
*   **Resilience:** Native **Redis Fallback** allows the system to continue operating via local memory if the Redis cluster becomes unavailable.
*   **Payload Protection:** Strict 1MB request limits and HTML sanitization prevent DoS and XSS attacks.

---

## 👤 User Experience (The Meta-Game)
AleaSim treats players as RPG characters to maximize Lifetime Value (LTV).

*   **Quest System:** Real-time missions (e.g., "Win $500 total", "Spin 100 times") with instant rewards.
*   **Leveling & VIP:** Dynamic XP accumulation based on wager volume, unlocking higher Cashback and better Raffle odds.
*   **Responsible Gaming:** Built-in controls for **Daily Loss Limits** and **Self-Exclusion** (24h to 30 days).
*   **Faucet Protection:** A secure "Bankruptcy Relief" system with distributed locking to prevent parallel request abuse.

> **[PLACEHOLDER: User Dashboard Screenshot]**

---

## 👑 Administrative God Mode
The Admin Panel provides absolute control over the platform's economics and security.

*   **Real-time RTP Control:** Adjust the global house edge and volatility on the fly.
*   **Simulation Suite:** Run 1,000,000+ spins in seconds to verify game math before deployment.
*   **Shadow Mode:** Compare "Pure RNG" vs "Brain Logic" outcomes in real-time without affecting live balances.
*   **System Integrity Repair:** One-click tool to re-sync balances and verify the audit ledger.
*   **Smart Replay:** Visual replay of any player round (Cards, Grid, Ball) directly from the audit logs.

> **[PLACEHOLDER: Admin Dashboard & Analytics Screenshot]**

---

## 📂 Technical Stack
*   **Backend:** .NET 8 WebAPI, Entity Framework Core (MySQL/MariaDB).
*   **Caching & Locks:** Redis (StackExchange.Redis) with Local Memory fallbacks.
*   **Real-time:** SignalR with Redis Backplane for 10,000+ concurrent connections.
*   **Frontend:** Blazor WebAssembly + MudBlazor UI + PixiJS (60FPS Game Animations).
*   **Automation:** Integrated Background Services for Raffles, Tournaments, and Audit Batching.

---

## 🚀 Deployment & Getting Started

### Environment Configuration (Kubernetes/Cloud)
The system is designed to read sensitive data from **Environment Variables** or **Secrets**:
*   `ConnectionStrings__DefaultConnection`: SQL Connection String
*   `ConnectionStrings__Redis`: Redis Connection String (with password support)
*   `Jwt__Key`: 256-bit security key for token signing

### Quick Start
1.  **Database:** Ensure MySQL/MariaDB is running. The system will automatically apply `Migrate()` or fallback to `EnsureCreated()`.
2.  **Run API:**
    ```bash
    cd AleaSim.Api && dotnet run
    ```
3.  **Run Client:**
    ```bash
    cd AleaSim.Client && dotnet run
    ```

---

## 🧪 Testing Strategy
The project maintains a high coverage test suite in `AleaSim.Tests`.
*   **Vault Tests:** Unit tests ensuring zero-sum financial integrity.
*   **RNG Tests:** Distribution analysis of the HMAC-SHA256 results.
*   **Brain Tests:** Verification of retention hooks and cooling logic.

```bash
dotnet test AleaSim.Tests/AleaSim.Tests.csproj
```

---
*© 2026 AleaSim Entertainment - Provably Fair, AI-Driven, Scalable.*
