# 🎲 AleaSim - Enterprise Gaming Simulation Platform

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Status](https://img.shields.io/badge/Status-Completed-success?style=flat-square)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20DDD-blue?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

> **A robust, deterministic, and auditable gambling simulation system built with .NET 8 and Blazor WebAssembly.**

## 📖 Project Overview

**AleaSim** is a high-performance full-stack system designed to simulate a real-world online casino environment. It is an engineering showcase demonstrating **enterprise-level architectural patterns**, **concurrency control**, **cryptographic auditing**, **probability mathematics**, and **modern UI/UX**.

The system is built on **"The Trinity"** architecture:
1.  **The Brain:** AI-driven retention engine that adapts difficulty and rewards based on player behavior (Flow State).
2.  **The Vault:** Secure financial core with atomic transactions and real-time balance synchronization.
3.  **The Engine:** Deterministic game logic with provably fair RNG and enforceable RTP (Return to Player) control.

---

## 🏗️ Architecture & Design

The solution follows **Clean Architecture** (Onion Architecture) principles.

### 1. AleaSim.Domain (The Core)
*   **Role:** Pure business logic, entities, and interfaces.
*   **Key Services:**
    *   `BrainService`: Manages player retention, dynamic difficulty, and personalized rewards.
    *   `VaultService`: Handles all financial transactions, ensuring solvency and atomic balance updates.
    *   `RtpEngine`: Enforces mathematical house edge using Feedback Loop Control.
    *   `Sentinel`: Background security monitor for fraud detection and anomaly analysis.

### 2. AleaSim.Persistence (Data Access)
*   **Role:** EF Core implementation of repositories.
*   **Database:** Designed for high integrity with `decimal(18,2)` precision and optimistic concurrency.

### 3. AleaSim.Api (The Backend)
*   **Role:** RESTful API and SignalR Hubs.
*   **Features:**
    *   **Real-Time:** Push notifications for Jackpots, Big Wins, Balance updates, and Chat.
    *   **Security:** JWT Auth, CORS policies, and Rate Limiting logic.
    *   **Background Workers:** Daily Bonuses, Tournament Payouts, and Security Scans.

### 4. AleaSim.Client (The Frontend)
*   **Technology:** **Blazor WebAssembly** with **MudBlazor** UI.
*   **Features:**
    *   **Interactive Games:** Slot (Clover Chase), Roulette, Blackjack with rich animations.
    *   **Social:** Global Chat with Avatars, Live Leaderboards, and "Big Win" celebrations.
    *   **RPG System:** Leveling, XP, Skill Tree (Perks), and Quests.
    *   **Admin Dashboard:** Live Session Monitor, Shadow Mode Analysis, RTP Control, and Player Inspector.

---

## 🌟 Key Features

### 🧠 AI & Retention (The Brain)
*   **Flow State Analysis:** Detects if a player is bored or frustrated and adjusts game volatility dynamically.
*   **Personalized Rewards:** "Lucky Symbol" tracking and custom daily quests.
*   **RPG Progression:** Players earn XP, level up, and unlock passive perks (Cashback, XP Boost, Luck).

### 🛡️ Security & Fairness
*   **Provably Fair:** Uses HMAC-SHA512. Players can verify every round's outcome against a server seed and nonce.
*   **Sentinel:** Automated fraud detection system that alerts admins of suspicious patterns (Bots, infinite money glitches).
*   **Audit Chain:** Immutable hash-chained ledger for all sensitive actions.

### 💰 Economy & Tournaments
*   **Live Tournaments:** Real-time ranking based on "Max Multiplier". Winners are automatically paid out monthly.
*   **Jackpots:** Local and Global progressive jackpots with "Must Drop" urgency visualizers.
*   **Voucher System:** Admin-generated codes for marketing campaigns.

---

## 🚀 How to Run

### Prerequisites
*   .NET 8 SDK
*   A SQL Database (MySQL/MariaDB) or use In-Memory for testing (configurable in `appsettings.json`).

### Steps
1.  **Clone the repository:**
    ```bash
    git clone https://github.com/YourRepo/AleaSim.git
    cd AleaSim
    ```

2.  **Start the Backend (API):**
    ```bash
    cd AleaSim.Api
    dotnet run
    ```
    *The API will start on `http://localhost:5286` (HTTP) to allow easy local dev connectivity.*

3.  **Start the Frontend (Client):**
    Open a new terminal.
    ```bash
    cd AleaSim.Client
    dotnet run
    ```
    *The Client will start on `https://localhost:7076`.*

4.  **Access the App:**
    Open your browser and navigate to `https://localhost:7076`.
    *   **User Login:** Register a new account.
    *   **Admin Login:** Use `admin` / `admin` (seeded automatically).

---

## 🛠️ Tech Stack

*   **Backend:** C# 12, .NET 8, ASP.NET Core Web API
*   **Frontend:** Blazor WebAssembly, MudBlazor
*   **Real-Time:** SignalR (WebSockets)
*   **Database:** Entity Framework Core (MySQL/PostgreSQL compatible)
*   **Security:** JWT, SHA-512, BCrypt
*   **Architecture:** Domain-Driven Design (DDD)

---

## 💡 Project Status
**COMPLETE**. The system is fully functional, including the "God Mode" admin tools, the "Shadow Mode" simulation engine, and the complete social/RPG loop.

*Ready for deployment or demonstration.*