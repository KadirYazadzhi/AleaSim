# Clover Chase - Game Design Document

## 1. Grid & Basic Rules
*   **Layout:** 5 Reels x 4 Rows (5x4).
*   **Paylines:** Standard Left-to-Right logic.
*   **Base Symbols:** Fruits (Lemon, Orange, Plum, Cherry, etc.) + Seven (7).
*   **Special Symbols:** 
    *   **Seven (7):** Wild for Fruits only.
    *   **Clover (Trigger/Wild):** Wild for EVERYTHING (Fruits + Sevens). Triggers Respin feature.
    *   **Bell (Bonus Only):** Appears only inside the Bonus Game (Hold & Win).

## 2. Base Game & The Clover Feature
The core mechanic is a **"Paid Sticky Respin"** loop triggered by Clovers.

### Trigger
*   Any single **Clover** landing on the reels triggers the feature.

### Respin Logic (The Chase)
1.  **Sticky:** The Clover locks in place.
2.  **Paid Spins:** The player is awarded **3 Respins**, but **THE BET IS DEDUCTED** for each spin (from user balance).
3.  **Wild Function:** During these spins, the stuck Clover acts as a **Universal Wild**.
    *   *Example:* [Clover] - [7] - [7] - [7] pays as **4 Sevens**.
4.  **Extension:**
    *   If a **NEW Clover** lands during the 3 respins:
        *   It also becomes Sticky (and Wild).
        *   The Respin counter resets back to **3**.
    *   If no new Clover lands and counter hits 0: The sticky Clovers are released, game returns to normal.

### Goal
*   Accumulate **5 or more Clovers** to trigger the **Bell Bonus Game**.

## 3. Bell Bonus Game (Hold & Win)
Triggered by having 5+ Sticky Clovers on screen.

### Transition
1.  All accumulated Clovers **flip/reveal** into **Bells**.
2.  Each Bell is assigned a random **Cash Value** (Multiplier: 0.2x to 100x Total Bet).

### Bonus Gameplay
*   **Grid:** Cleared of all fruits/sevens. Only the triggering Bells remain.
*   **Spins:** Player gets **3 Free Spins** (No bet deducted here).
*   **Mechanic:** Reels spin containing only **New Bells** or **Blanks**.
*   **Reset:** If a new Bell lands:
    *   It locks in place.
    *   It gets a Cash Value.
    *   Spin counter resets to **3**.

### Special Bells (Mini / Minor)
*   **Appearance:** Distinct visual look.
*   **Value:** Fixed constant amounts based on denomination (e.g., Mini = 20 BGN, Minor = 100 BGN).
*   **Value Logic:** Their value is ADDED to the standard Bell value pool.
*   **Probability Algorithm (Diminishing Returns):**
    *   Chance to spawn depends on Bet Size (Higher Bet = Lower Chance?).
    *   **Hard Constraint:** Chance drops exponentially with each existing Mini/Minor on screen.
    *   *Rule:* Max ~5 Mini/Minor bells per game (soft cap via probability -> 0.0001%).

### End Game & Multipliers
The bonus ends when lives = 0 or screen is full.

1.  **Collection:** Sum up all Bell values (Cash + Mini + Minor).
2.  **Global Multiplier:**
    *   **15+ Bells:** Total Win x **2**.
    *   **20 Bells (Full Screen):** Total Win x **3**.

## 5. Denominations & Jackpots

### Denomination Logic
The game operates on a Credit system tied to real currency.
*   **Supported Denominations (BGN):** 0.01, 0.02, 0.05, 0.10, 0.20, 0.50, 1.00.
*   **Total Bet Calculation:** `BetCredits * Denomination`.
    *   *Example:* 100 Credits at 0.01 Denom = **1.00 BGN Bet**.
    *   *Example:* 100 Credits at 1.00 Denom = **100.00 BGN Bet**.

### Fixed Jackpot Values (Mini / Minor)
These special bells have constant values based on the **Denomination**, not the Total Bet.

| Jackpot | Multiplier (x Denom) | Value at 0.01 | Value at 1.00 | Probability | Cap |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Mini** | 1000x | **10.00 BGN** | **1,000.00 BGN** | High | Soft Cap (exponential decay) |
| **Minor** | 5000x | **50.00 BGN** | **5,000.00 BGN** | Very Low (~1/5 of Mini) | **Hard Cap at 3** (0% chance after 3rd) |

### Logic Implications
*   Players are incentivized to increase **Denomination** to chase bigger Jackpots.
*   Playing at Max Bet on Low Denom (e.g., 1000 credits at 0.01) gives a huge base game win potential but keeps Jackpots small (10/50 BGN).
*   Playing at Min Bet on High Denom (e.g., 1 credit at 1.00) gives small base wins but huge Jackpots (1000/5000 BGN).

## 6. Technical Requirements & Edge Cases
*   **State Machine:** Must track `RespinState` (How many clovers, position, lives left) between HTTP requests.
*   **Bet Change Logic (SIMPLIFIED):**
    *   **Rule:** When a Clover lands and becomes sticky, the Bet Amount is **LOCKED**.
    *   **UI Behavior:** The "Change Bet" buttons must be disabled on the client side.
    *   **Backend Validation:** If a request comes with a different bet amount while `IsFeatureActive` is true, the server returns an Error ("Bet change not allowed during active feature").
    *   **Unlock:** The bet controls are unlocked only when the feature ends (either by running out of lives or completing the Bonus Game) and the board is cleared.
*   **RTP Control:** 
    *   The "Paid Respin" phase has high RTP due to Sticky Wilds. Math must account for this.
    *   Bonus Game values must be generated *after* checking the `RtpEngine` (Pool Balance). If Pool is low, generate low multipliers (0.2x).
