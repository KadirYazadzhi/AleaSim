# Core Architecture: The "Casino Brain"

## Overview
To simulate a real-world Class III / Server-Based Gaming environment, AleaSim is divided into three distinct logical layers.

## 1. The Game Director ("The Brain")
*   **Role:** Strategy, User Profiling, AI.
*   **Responsibility:** Decides *HOW* the game should play before the spin happens.
*   **Inputs:**
    *   User History (LTV, Churn Risk).
    *   Session Analytics (Time on Device, Recent Losses).
    *   Global Events (Jackpot Triggers).
*   **Outputs:** `SpinProfile` (High Volatility, Teaser, Retention Mode).

## 2. The Game Engine ("The Executor")
*   **Role:** Mechanics, Rules, RNG.
*   **Responsibility:** Executes the spin based on the `SpinProfile`.
*   **Logic:**
    *   Generates Random Numbers.
    *   Applies Game Rules (Lines, Wilds, Bonuses).
    *   Executes "Near Miss" visualization if requested by Brain or forced by Cashier.
*   **State:** Stateless for Base Game, Stateful for Bonus Rounds (persisted in DB).

## 3. The RTP Engine ("The Cashier")
*   **Role:** Accounting, Pool Management.
*   **Responsibility:** Financial final say.
*   **Logic:**
    *   **Pool Based (Compensated):** `Money In` adds to Pool. `Money Out` subtracts.
    *   **Authorization:** If `Pool < WinAmount`, the payout is DENIED.
    *   **Fallback:** If denied, signals the Game Engine to re-roll or show a Teaser.

## Interaction Flow
1.  **Request:** User hits SPIN.
2.  **Brain:** `GameDirector` analyzes user -> Selects `SpinProfile.HighVolatility`.
3.  **Executor:** `GameEngine` runs RNG -> Hits a 500x Win.
4.  **Cashier:** `RtpEngine` checks Pool -> "Insufficient Funds".
5.  **Fallback:** `GameEngine` changes result to "Near Miss" (Teaser).
6.  **Response:** User sees a thrilling (but losing) spin.
