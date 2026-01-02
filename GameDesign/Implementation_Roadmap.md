# 🛣️ Implementation Roadmap: The Trinity Architecture

This document outlines the step-by-step implementation plan to transform AleaSim into a behavior-driven casino platform using the **Trinity Architecture** (Brain, CMS, Vault).

---

## Phase 1: Domain Entities & Database Foundation
*Goal: Prepare the data structure to support profiling, separate wallets (Real/Bonus), and detailed game history.*

1.  **Modify `User.cs`:**
    *   Add **Wallet Management** fields:
        *   `BonusBalance`: Money locked for wagering.
        *   `WageringRequirement`: Target amount to bet to unlock bonus.
        *   `WageringProgress`: Amount currently bet towards the target.
    *   Add **Activity Tracking**:
        *   `LastBetTimestamp`: Critical for "Active Player" check in raffles.
2.  **Create `PlayerProfile.cs` (New Entity):**
    *   A 1-to-1 relation with `User`.
    *   Stores behavioral data for The Brain:
        *   `VolatilityScore`: Preference for risk.
        *   `ChurnRiskScore`: Probability of leaving.
        *   `LTV`: Lifetime Value (Net Deposit).
        *   `CurrentSessionRtp`: To track "Luck" in real-time.
3.  **Modify `GameRound.cs`:**
    *   Add context for *WHY* the result happened.
    *   `DecisionType`: (e.g., "Random", "RetentionHook", "WhaleBonus").
    *   `TargetWin`: The amount The Brain requested.

---

## Phase 2: The Logic Core (Brain & Vault)
*Goal: Implement the services that make decisions and manage money.*

4.  **Create `VaultService` (Financial Controller):**
    *   Replaces/Augments `RtpEngine`.
    *   Manages `ShadowWallet` (Personal RTP tracking).
    *   Handles **Dual-Wallet Transactions** (Real vs Bonus logic).
    *   Methods: `ProcessBet(user, amount)`, `CanAffordWin(user, amount)`.
5.  **Create `BrainService` (Decision Engine):**
    *   **Analyzer:** `AnalyzePlayerContext(userId)` (Reads Profile & Session).
    *   **Decision Maker:** `DecideOutcome(userId, gameId)` -> Returns a Directive.
    *   **Strategies:** Implement logic for "Sugar Hit" (Retention), "Cool Down" (Safety), and "Whale Protocol".

---

## Phase 3: Game Engines Refactoring (Reverse Engineering)
*Goal: Convert game engines from "Random Generators" to "Asset Mappers".*

6.  **Refactor `SlotGameEngine.cs`:**
    *   Remove pure RNG loop.
    *   Implement `PatternGenerator`: Accepts a `TargetWin` (from Brain) and finds symbol combinations that match it.
    *   Implement `NearMissGenerator`: Visuals for "Loss" that look close to "Win".
7.  **Refactor `RouletteGameEngine.cs`:**
    *   Implement logic to force ball position based on the aggregate payout limit set by The Brain.

---

## Phase 4: Promotions & Tournaments
*Goal: Add retention mechanics.*

8.  **Create `PromotionService`:**
    *   Raffle Logic: Ticket accumulation (50 wagered = 1 ticket).
    *   Tournament Logic: Live tracking of Profit Multipliers.
9.  **Implement `RaffleScheduler`:**
    *   Logic for "Time-Gated Drops" (Sunday 19:00-21:00).
    *   Active Player Check (`LastBetTimestamp` < 3 mins).
    *   Re-roll mechanics for offline winners.

---

## Phase 5: Integration & Orchestration
*Goal: Wire everything together.*

10. **Update `GameDirector`:**
    *   New Flow: `Bet` -> `Vault (Deduct)` -> `Brain (Decide)` -> `CMS (Visualize)` -> `Vault (Credit)` -> `Promo (Update)`.
