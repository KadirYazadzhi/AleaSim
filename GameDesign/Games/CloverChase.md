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
*   **Value:** Fixed constant amounts based on denomination (e.g., Mini = 20 USD, Minor = 100 USD).
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
*   **Supported Denominations (USD):** 0.01, 0.02, 0.05, 0.10, 0.20, 0.50, 1.00.
*   **Total Bet Calculation:** `BetCredits * Denomination`.
    *   *Example:* 100 Credits at 0.01 Denom = **1.00 USD Bet**.
    *   *Example:* 100 Credits at 1.00 Denom = **100.00 USD Bet**.

### Fixed Jackpot Values (Mini / Minor)
These special bells have constant values based on the **Denomination**, not the Total Bet.

| Jackpot | Multiplier (x Denom) | Value at 0.01 | Value at 1.00 | Probability | Cap |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Mini** | 1000x | **10.00 USD** | **1,000.00 USD** | High | Soft Cap (exponential decay) |
| **Minor** | 5000x | **50.00 USD** | **5,000.00 USD** | Very Low (~1/5 of Mini) | **Hard Cap at 3** (0% chance after 3rd) |

### Logic Implications
*   Players are incentivized to increase **Denomination** to chase bigger Jackpots.
*   Playing at Max Bet on Low Denom (e.g., 1000 credits at 0.01) gives a huge base game win potential but keeps Jackpots small (10/50 USD).
*   Playing at Min Bet on High Denom (e.g., 1 credit at 1.00) gives small base wins but huge Jackpots (1000/5000 USD).

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

---

## 7. Paytable

All values are **multipliers of the Total Bet** for a matching payline combination.

| Symbol | 3-of-a-Kind | 4-of-a-Kind | 5-of-a-Kind |
| :--- | :---: | :---: | :---: |
| **Clover** (Wild) | 5x | 20x | 100x |
| **Seven (7)** | 4x | 15x | 75x |
| **Bell** (Bonus only) | — | — | — |
| **Cherry** | 1x | 5x | 25x |
| **Plum** | 0.8x | 4x | 20x |
| **Orange** | 0.6x | 3x | 15x |
| **Lemon** | 0.4x | 2x | 10x |

> **Notes:**
> - The Clover acts as a Universal Wild and substitutes for every symbol including the Seven.
> - Bell symbols have no payline value — their cash value is assigned individually during the Bell Bonus Game.
> - Pays are evaluated left-to-right on each of the configured paylines.

---

## 8. Visual ASCII Grid

The game plays on a **5 Reel × 4 Row** grid (20 symbol positions total).

```
       Reel 1   Reel 2   Reel 3   Reel 4   Reel 5
Row 1 [ Lemon ] [  7  ] [ 🍒  ] [ Plum ] [  7  ]
Row 2 [ 🍊   ] [Clover] [ 🍋  ] [  7  ] [Cherry]
Row 3 [ Plum  ] [ 🍋  ] [  7  ] [Clover] [ Plum ]
Row 4 [  7   ] [ 🍊  ] [ Plum] [ 🍒  ] [ Lemon]
```

**Example Respin State** — Two Clovers locked, 3 lives remaining:

```
       Reel 1   Reel 2   Reel 3   Reel 4   Reel 5
Row 1 [ Lemon ] [  7  ] [ 🍒  ] [ Plum ] [  7  ]
Row 2 [ 🍊   ] [★CLV★] [ 🍋  ] [  7  ] [Cherry]   ← Sticky Clover (Reel 2, Row 2)
Row 3 [ Plum  ] [ 🍋  ] [  7  ] [★CLV★] [ Plum ]   ← Sticky Clover (Reel 4, Row 3)
Row 4 [  7   ] [ 🍊  ] [ Plum] [ 🍒  ] [ Lemon]
```

`★CLV★` = Locked Sticky Clover Wild (does not spin, acts as universal wild for payline evaluation).

---

## 9. State Machine Diagram

Text-based flowchart of the full Clover Chase respin and bonus flow:

```
                        ┌───────────────┐
                        │   BASE GAME   │  ← Normal spin, standard RNG
                        └───────┬───────┘
                                │
                    Clover lands on reels?
                      ┌─────────┴─────────┐
                     YES                  NO
                      │                   │
                      ▼                   ▼
               ┌─────────────┐     Continue base game
               │ CLOVERLANDS │  ← Clover locked; lives = 3
               │  (Entry)    │
               └──────┬──────┘
                      │
               Spin (bet deducted)
                      │
              ┌───────┴────────┐
          New Clover?        No new Clover
              │                    │
              ▼                    ▼
   ┌──────────────────┐   ┌─────────────────┐
   │  RESPIN ACTIVE   │   │ DECREMENT LIVES │
   │  Reset lives = 3 │   │  lives = lives-1 │
   │  Lock new Clover │   └────────┬────────┘
   └──────────┬───────┘            │
              │              lives > 0?
              │           ┌────────┴────────┐
              │          YES               NO
              │           │                 │
              │     Loop back to       ┌────▼──────────┐
              │     "Spin (bet         │ RETURN TO BASE│
              │      deducted)"        │  Release all  │
              │                        │  Clovers;     │
              │                        │  eval paylines│
              │                        └───────────────┘
              │
   Total Clovers locked ≥ 5?
       ┌──────┴──────┐
      YES            NO
       │              │
       ▼          Continue Respin loop
┌─────────────────┐
│ BELL BONUS GATE │  ← All Clovers flip to Bells with random cash values
└────────┬────────┘
         │
         ▼
┌──────────────────────┐
│  BELL BONUS ACTIVE   │  ← 3 free spins (no bet deducted)
│  Reel: Bells + Blank │     New Bell lands → lock + reset to 3
└──────────┬───────────┘
           │
    Lives = 0 OR grid full?
           │
           ▼
  ┌────────────────────┐
  │  COLLECT & RESOLVE │  ← Sum Bells; apply 2x (15+ Bells) or 3x (20 Bells)
  └────────────────────┘
           │
           ▼
  Return to BASE GAME
```
