# 🎲 AleaSim - Enterprise Gaming Simulation Platform

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Status](https://img.shields.io/badge/Status-Backend%20Complete-success?style=flat-square)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20DDD-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

> **A robust, deterministic, and auditable gambling simulation system built with .NET 8.**

## 📖 Project Overview

**AleaSim** is a high-performance backend system designed to simulate a real-world online casino environment. It is not merely a game; it is an engineering showcase demonstrating **enterprise-level architectural patterns**, **concurrency control**, **cryptographic auditing**, and **probability mathematics**.

The primary goal is to demonstrate how to build a system that is:
1.  **Deterministic:** Every game outcome can be strictly reproduced given the initial seed, allowing for retroactive replay and verification.
2.  **Fair & Controlled:** Strictly enforced **RTP (Return to Player)** mechanics ensure the "House" maintains its mathematical edge while allowing for short-term player variance.
3.  **Auditable:** An immutable, hash-chained ledger records every critical action, making data tampering mathematically detectable.
4.  **Real-Time:** Instant state propagation to clients using **SignalR** (WebSockets).

---

## 🏗️ Architecture & Design

The solution follows **Clean Architecture** (Onion Architecture) principles, enforcing strict separation of concerns and dependency rules.

### 1. AleaSim.Domain (The Core)
*   **Role:** Contains pure business logic, entities, interfaces, and game engines.
*   **Dependencies:** None. It is independent of the database, UI, or external frameworks.
*   **Key Components:**
    *   `GameEngine`: Abstract base implementing the Template Method pattern for session lifecycles.
    *   `RtpEngine`: The mathematical "brain" that monitors payout ratios. It uses **Atomic Locks** to prevent race conditions during high-concurrency win processing.
    *   `DeterministicRngService`: A custom RNG service ensuring cross-platform reproducibility.
        *   *Algo:* Uses a stable bitwise XOR shift `((seed * 397) ^ sequence)` to generate entropy, avoiding platform-dependent `HashCode` implementations.
    *   `AuditService`: Implements a "Blockchain-lite" structure where every log entry contains the SHA-256 hash of the previous one.

### 2. AleaSim.Persistence (Data Access)
*   **Role:** Implements the interfaces defined in the Domain using **Entity Framework Core**.
*   **Key Components:**
    *   `EfGameRepository`: Handles database transactions and optimistic concurrency.
    *   `EfTransactionWrapper`: Abstraction for transaction scopes, allowing services to commit/rollback atomic units of work.
    *   **Database Schema:** Optimized for transactional integrity. Uses `decimal(18,2)` for all monetary values to prevent floating-point rounding errors.

### 3. AleaSim.Api (The Interface)
*   **Role:** Exposes the functionality via RESTful Endpoints and Real-Time Hubs.
*   **Key Components:**
    *   **JWT Authentication:** Secure, stateless access control using Bearer tokens.
    *   **SignalR Hubs:** Pushes Jackpot updates and Game results to specific users (Privacy-aware) and broadcast channels.
    *   **Factory Pattern:** Uses `Func<string, IGame>` to dynamically resolve game engines (Slot, Roulette, Blackjack) at runtime based on request parameters.

---

## ⚙️ Deep Dive: Core Mechanics

### 🧠 The RTP Engine (Return to Player)
The system doesn't just rely on randomness. It implements a **Feedback Loop Control System** to ensure regulatory compliance.

**The Algorithm:**
1.  **Tracking:** The system tracks `TotalWagered` and `TotalPaid` globally, per-game, and per-user.
2.  **Projection:** Before confirming a win, the engine calculates:
    $$ 	ext{Projected RTP} = rac{	ext{TotalPaid} + 	ext{PotentialWin}}{	ext{TotalWagered} + 	ext{CurrentBet}} $$
3.  **Enforcement:** If `Projected RTP > Target RTP + Deviation` (e.g., 95% + 5%), the win is **rejected** (zeroed out) to enforce the house edge.
4.  **Atomicity:** The check and the record update happen in a single `lock` block within `ProcessWin`. This prevents "Time-of-Check to Time-of-Use" (TOCTOU) race conditions where multiple parallel bets could bypass the limit.

### 🔒 Cryptographic Audit Trail
To satisfy "Regulatory" requirements (simulated), the system employs an append-only log:
*   **Structure:** `Timestamp | EventType | UserId | Metadata | PreviousHash`
*   **Hashing:** SHA-256
*   **Integrity:** `Hash_n = SHA256(Data_n + Hash_{n-1})`
*   **Result:** If a database administrator tries to delete or modify a past record, the hash of all subsequent records becomes invalid, alerting the system to tampering.

### 🎰 The Jackpot System
*   **Dual-Layer:**
    *   **Local Jackpot:** Specific to a single game instance. High hit frequency, lower value.
    *   **Global Jackpot:** Shared across all games. Low hit frequency, high value.
*   **Contribution:** A percentage of every bet (e.g., 1%) acts as a "tax" that feeds the pools.
*   **Triggering:** Uses a secondary RNG roll. If triggered, the pot is awarded, reset to a seed value, and all clients are notified via SignalR.

---

## 🎮 Game Logic & Rules

### 🍒 Slot Machine
*   **Reels:** 3 Reels with 8 symbols each.
*   **Logic:**
    1.  RNG selects stop positions for each reel.
    2.  Symbols are mapped from virtual strips.
    3.  Wins are calculated based on a fixed Paytable (e.g., 3x Cherry = 10x Bet).
    4.  Final win is validated against the RTP Engine.

### 🃏 Blackjack
*   **Type:** Simplified "Player vs Dealer".
*   **Rules:**
    *   Dealer must hit on 16, stand on 17.
    *   Blackjack pays 3:2 (simulated 2.5x).
    *   Standard Push (Tie) returns bet.
*   **State Machine:** Maintains game state (`PlayerHand`, `DealerHand`, `Sequence`) between API calls (`Hit`, `Stand`).

### 🎡 European Roulette
*   **Wheel:** Single Zero (0-36).
*   **Bets Supported:**
    *   Specific Number (Pays 35:1).
    *   Red/Black (Pays 1:1).
    *   Even/Odd (Pays 1:1).
*   **Math:** Uses standard probability (1/37 for straight up).

---

## 📂 Project Structure

```text
/AleaSim
├── AleaSim.Domain/          # Pure Business Logic
│   ├── Entities/            # Database Models (User, Bet, Game, AuditEvent)
│   ├── Services/            # Core Algorithms (RtpEngine, AuditService, RngService)
│   ├── Interfaces/          # Abstractions (IGame, IRepository)
│   └── Enums/               # Fixed types (Role, EventType)
│
├── AleaSim.Persistence/     # Infrastructure
│   ├── Configurations/      # EF Core Mappings (Table definitions)
│   └── Repositories/        # EfGameRepository, TransactionWrapper
│
├── AleaSim.Api/             # Presentation Layer
│   ├── Controllers/         # Auth, Game, Admin Controllers
│   ├── Hubs/                # GameHub (SignalR)
│   └── Program.cs           # Dependency Injection Setup
│
└── AleaSim.Client/          # (Planned) Blazor WebAssembly Frontend
```

---

## 🔌 API Reference (Key Endpoints)

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Returns JWT Token for session access. |
| `POST` | `/api/game/{type}/session` | Starts a new game session (Slot, Roulette, etc.). |
| `POST` | `/api/game/{type}/bet/{id}` | Places a bet. Returns Outcome and Win Amount. |
| `POST` | `/api/game/{type}/action/{id}`| Performs in-game action (Hit/Stand for Blackjack). |
| `GET` | `/api/admin/audit-logs` | Retrieves the immutable audit chain (Admin only). |

---

## 🚀 Roadmap & Status

### Phase 1: Backend Core (✅ Completed)
- [x] Domain Modeling & Clean Architecture.
- [x] Game Engines (Slot, Roulette, Blackjack).
- [x] **Secure RNG:** Upgraded to `Crypto.RandomNumberGenerator` for seeding.
- [x] **RTP Control:** Atomic "Check-and-Set" logic implemented.
- [x] **Concurrency:** Fixed race conditions in Jackpot and RTP services.
- [x] **Auditing:** Hash-chain integrity verified.
- [x] **Real-Time:** Privacy-aware SignalR notifications.

### Phase 2: Frontend (🚧 Planned)
- [ ] **Technology:** **Blazor WebAssembly** (C#).
- [ ] **Goal:** Full-stack .NET solution.
- [ ] **Features:**
    - Shared DTOs between Backend and Frontend.
    - Interactive Game Board components.
    - Live Jackpot Ticker.
    - Admin Dashboard for visualizing the Audit Chain.

---

## 🛠️ Tech Stack

*   **Language:** C# 12
*   **Framework:** .NET 8
*   **Web API:** ASP.NET Core
*   **Real-Time:** SignalR
*   **Database:** MySQL / PostgreSQL (via EF Core)
*   **Security:** JWT, SHA-256, CSPRNG
*   **Architecture:** Domain-Driven Design (DDD) Lite

---

## 💡 Engineering Challenges Solved

1.  **The "Infinite Money" Glitch:** By enforcing atomic RTP checks, we prevent users from exploiting API lag to place bets that exceed the system's payout capacity.
2.  **The "Rogue Admin" Problem:** Even with DB access, an admin cannot alter a past game result without invalidating the cryptographic hash chain of all subsequent logs.
3.  **The "Platform" Problem:** RNG logic often behaves differently on Linux vs Windows. We replaced `HashCode.Combine` with stable bitwise math to guarantee that `Seed: 123` always produces the same game sequence on any OS.

---
*Created by Anonymous User for Portfolio Purposes.*