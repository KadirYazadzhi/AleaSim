# 🎲 AleaSim - Enterprise Gaming Simulation Platform

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Status](https://img.shields.io/badge/Status-Beta%20%2F%20Active-yellow?style=flat-square)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20DDD-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

> **A robust, deterministic, and auditable gambling simulation system built with .NET 8 and Blazor WebAssembly.**

## 📖 Documentation Hub

*   **[🎮 Game Mechanics & Logic](docs/GameMechanics.md)**: Detailed rules for Slot, Roulette, and Blackjack.
*   **[🏗️ Architecture & Modules](docs/Architecture_And_Modules.md)**: Deep dive into the Domain, API, and Client structure.
*   **[🧠 The Brain & Retention](docs/Brain_And_Retention.md)**: How the AI adjusts difficulty and RTP.
*   **[🚧 Challenges & Roadmap](docs/Development_Challenges_And_Roadmap.md)**: Known bugs, fixed issues, and future plans.

---

## 🚀 Project Overview

**AleaSim** is a high-performance full-stack system designed to simulate a real-world online casino environment. It is an engineering showcase demonstrating **enterprise-level architectural patterns**, **concurrency control**, **cryptographic auditing**, and **modern UI/UX**.

The system is built on **"The Trinity"** architecture:
1.  **The Brain:** AI-driven retention engine that adapts difficulty and rewards based on player behavior (Flow State).
2.  **The Vault:** Secure financial core with atomic transactions and distributed locking.
3.  **The Engine:** Deterministic game logic with provably fair RNG and enforceable RTP (Return to Player) control.

---

## 🌟 Key Features

### 🎰 Games
*   **Clover Chase (Slot):** 5-reel slot with Hold & Win Bonus, Sticky Respins, Mystery Nudge, and Gamble feature.
*   **European Roulette:** Classic single-zero roulette with complex betting logic and state recovery.
*   **Blackjack:** Feature-complete Blackjack with Split, Double Down, and Dealer rules.

### 🧠 Intelligence
*   **Flow State:** Adjusts game speed and volatility to keep players "in the zone".
*   **Retention Hooks:** Automatically detects losing streaks and intervenes (if affordable) to prevent churn.
*   **Shadow Balance:** Tracks "Personal RTP" to ensure fair distribution of luck over time.

### 🛡️ Security
*   **Concurrency Control:** Uses `ILockService` (Semaphore/Redis) to prevent **Double Spend** attacks.
*   **Provably Fair:** HMAC-SHA512 hashing of server seeds allows players to verify every result.
*   **Sentinel:** Background monitoring for anomalies.

---

## 🛠️ Tech Stack

*   **Backend:** C# 12, .NET 8, ASP.NET Core Web API
*   **Frontend:** Blazor WebAssembly, MudBlazor
*   **Real-Time:** SignalR (WebSockets)
*   **Database:** Entity Framework Core (MySQL)
*   **Security:** JWT, SHA-512, ILockService
*   **Architecture:** Domain-Driven Design (DDD)

---

## 🚀 How to Run

1.  **Backend:**
    ```bash
    cd AleaSim.Api
    dotnet run
    ```
    *Starts on `http://localhost:5286`*

2.  **Frontend:**
    ```bash
    cd AleaSim.Client
    dotnet run
    ```
    *Starts on `https://localhost:7076`*

3.  **Credentials:**
    *   **Admin:** `admin` / `admin`
    *   **User:** Register a new account.

---

## ⚠️ Current Status: Beta
The system is functional but in active development.
*   **Known Issues:** Audio assets might be missing in the repo (placeholders used).
*   **Simulation:** Use the "Simulation Mode" API to stress-test the math (run 100k spins in seconds).

---
*Created by an AI-Augmented Engineering Team.*
