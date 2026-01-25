# 🎰 AleaSim: The AI-Driven Casino Simulation

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-WASM-5C2D91?style=flat-square&logo=blazor)
![Architecture](https://img.shields.io/badge/Architecture-Clean%20%2F%20DDD-blue?style=flat-square)
![AI Engine](https://img.shields.io/badge/AI-Brain%20Engine-FF4500?style=flat-square&logo=openai)
![Status](https://img.shields.io/badge/Status-Beta%20(Active%20Dev)-yellow?style=flat-square)
![Tests](https://img.shields.io/badge/Tests-Passing-success?style=flat-square)

> **A massive, enterprise-grade gambling simulation platform powered by a behavioral AI "Brain" that dynamically adjusts volatility, RTP, and player retention in real-time.**

---

## 🧠 The Core Innovation: "The Brain" (AI Engine)

AleaSim is not just a random number generator. It features a sophisticated **Artificial Intelligence layer (`BrainService`)** that acts as an invisible Game Director.

### How the AI Works:
1.  **Flow State Detection:** The AI measures the time between spins (in milliseconds).
    *   *Too Fast (<2.5s):* Player is engaged. The AI increases volatility (bigger wins, longer dry spells) to maintain adrenaline.
    *   *Too Slow (>7s):* Player is bored. The AI switches to "Popcorn Mode" (frequent small wins) to re-engage attention.
2.  **Retention Hooks (Anti-Churn):**
    *   If the AI detects a dangerous "Loss Streak" (e.g., >8 losses), it calculates the player's **Churn Risk**.
    *   It can override the RNG to force a "Near Miss" (Teaser) or a specific "Bonus Trigger" to keep the player on the platform, provided the **Vault** can afford it.
3.  **Shadow Balance (Personal RTP):**
    *   The system tracks a "Shadow Wallet" for every user – the amount they *mathematically should have won*.
    *   The AI uses this to balance luck over time, ensuring no player is unfairly destroyed and the casino remains solvent.

---

## 🎮 The Games

All games run on a **Deterministic, Provably Fair Engine**.

| Game | Unique Mechanics | AI Integration |
| :--- | :--- | :--- |
| **🍀 Clover Chase** (Slot) | • **Hold & Win Bonus** (3 Lives)<br>• **Sticky Respins** (Wilds lock)<br>• **Mystery Nudge** (The Juice)<br>• **Gamble** (Red/Black) | AI decides when to "Nudge" a losing reel into a win. AI controls the Bonus frequency based on budget. |
| **🔴 European Roulette** | • **Track Bets**<br>• **Complex Patterns** (Orphans, Neighbors)<br>• **State Recovery** (Resume after refresh) | AI can force the ball away from heavily covered numbers if RTP is critical, or force a hit on a "Favorite Number". |
| **♠️ Blackjack** | • **Perfect Dealer Logic**<br>• **Split & Double Down**<br>• **Insurance** | Dealer logic is strictly math-based, but AI tracks "Card Counting" behavior via bet sizing patterns. |

---

## 🏗️ System Architecture (The "Trinity")

The system is built on three pillars, following strictly enforced **Clean Architecture**:

1.  **🏛️ The Vault (Finance):**
    *   Atomic Transactions.
    *   **Distributed Locking (`ILockService`):** Prevents "Double Spend" attacks where a user spins 50 times in 1 millisecond.
    *   **Bonus Wallet:** Separate wagering requirements logic.
2.  **🧠 The Brain (Intelligence):**
    *   Decoupled service that intercepts every game round.
    *   Returns `BrainDirective` objects (e.g., `ForceWin`, `CoolDown`, `Random`).
3.  **⚙️ The Engine (Physics):**
    *   `SlotGameEngine`, `RouletteGameEngine`, etc.
    *   Stateless logic that accepts a Seed and a Directive to produce a Result.

---

## 📂 Project Structure & Functionality

The solution is split into specialized sub-projects. [See Detailed Structure Docs](docs/Project_Structure.md).

*   **`AleaSim.Domain`**: The "Holy Grail". Contains Entities (`User`, `GameRound`), Interfaces, and the Core Services (`Vault`, `Brain`). **Zero external dependencies.**
*   **`AleaSim.Persistence`**: Entity Framework Core implementation. Handles MySQL connections, heavy `JOIN` queries for history, and Transaction scopes.
*   **`AleaSim.Api`**: The REST Interface.
    *   **SignalR Hubs:** Pushes real-time Jackpots and Balance updates.
    *   **Background Workers:** Runs the Raffle, Daily Bonus, and Tournament Payouts.
    *   **Middleware:** Global Error Handling.
*   **`AleaSim.Client`**: Blazor WebAssembly (The UI).
    *   **PixiJS Interop:** Renders the 60FPS slot animations.
    *   **AudioService:** Manages dynamic soundscapes.
    *   **State Containers:** Handles Game Recovery and reactive UI.

---

## 🚧 Current Status & Known Issues (Beta)

**Completion:** ~85%
**Stability:** High (Backend), Medium (Frontend)

While the core math and banking logic are production-ready, the system has known issues typical of a Beta release.

### 🐛 Critical Bugs & Missing Features
1.  **🔊 Audio Context:** Browsers block auto-play audio. Users must interact with the page first.
2.  **🖼️ Asset 404s:** Some placeholder images for Avatars or Slot Symbols may be missing in the repo.
3.  **🔒 Auth Noise:** The server logs spam `Authorization failed` warnings for non-critical admin checks.
4.  **📱 Mobile UI:** The Slot Machine canvas does not resize perfectly on vertical mobile screens.
5.  **🐌 Database Deadlocks:** Under extreme simulation load (>1000 spins/sec), the `BrainService` and `GameEngine` sometimes fight for a row lock (Fixed in recent patch, but monitoring needed).
6.  **📉 Performance:** `GetUserHistory` can be slow for users with >100k spins (Needs Pagination optimization).
7.  **🛑 Redis:** Currently using `MemoryCache` and `SemaphoreSlim`. Needs Redis for multi-server scaling.
8.  **🃏 Blackjack UI:** Split animations are instant/jarring.
9.  **📧 Emails:** Registration does not send real emails (Service is mocked).
10. **💳 Payments:** Deposit/Withdrawal buttons are UI-only mocks.

[View Full Roadmap & Bug Tracker](docs/Development_Challenges_And_Roadmap.md)

---

## 🚀 Getting Started

### 1. Prerequisites
*   .NET 8 SDK
*   MySQL / MariaDB (or use In-Memory config)

### 2. Run the Backend
```bash
cd AleaSim.Api
dotnet run
# Listening on http://localhost:5286
```

### 3. Run the Client
```bash
cd AleaSim.Client
dotnet run
# Listening on https://localhost:7076
```

### 4. Admin Access
*   **User:** `admin`
*   **Pass:** `admin`
*   **God Mode:** Go to `/admin/dashboard` to control RTP, inject money, or run Simulations.

---

### 🧪 Testing
The project includes a comprehensive Test Suite (`AleaSim.Tests`) covering:
*   ✅ **Vault Integrity:** Ensuring money is never lost.
*   ✅ **RNG Distribution:** Verifying randomness over 1M iterations.
*   ✅ **Brain Logic:** Ensuring Retention Hooks fire correctly.

```bash
dotnet test
```
