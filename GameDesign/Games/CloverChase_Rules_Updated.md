# Clover Chase - Detailed Rules & Mechanics (Updated)

## 1. Symbol Hierarchy & Wild Logic
*   **The Clover (Universal Wild):**
    *   Substitutes for **ALL** symbols (Fruits AND Sevens).
    *   *Example:* [Clover] - [7] - [7] = 3 Sevens Paid.
    *   *Example:* [Clover] - [Lemon] - [Lemon] = 3 Lemons Paid.
*   **The Seven (Partial Wild):**
    *   Substitutes for **FRUITS ONLY**.
    *   Does **NOT** substitute for Clover.
    *   *Example:* [7] - [Lemon] - [Lemon] = 3 Lemons Paid.
*   **Anti-Frustration Logic:**
    *   The RNG/CMS should avoid generating high-value symbols (like 4 Sevens) starting from Reel 2 if Reel 1 is a miss. This creates a "Near Miss" that feels unfair rather than exciting.

## 2. Pay Logic
*   **Direction:** Left to Right ONLY (Reel 1 -> Reel 5).
*   **Requirement:** Minimum 3 consecutive symbols starting from Reel 1.

## 3. Bell Bonus Game (Values & Probabilities)
Triggered by 5+ Clovers. Clovers turn into Bells.

### Bell Types
1.  **Cash Bell:**
    *   Value: Multiplier of Total Bet (0.2x, 0.5x, 1x ... up to 100x).
2.  **Mini Jackpot Bell:**
    *   Value: Fixed constant per Denomination.
    *   **Probability:** Decreases exponentially with each Mini bell already on screen.
    *   *Soft Cap:* Around 5 Minis.
3.  **Minor Jackpot Bell:**
    *   Value: Higher fixed constant.
    *   **Probability:** Starts lower than Mini. Decreases sharply.
    *   *Hard Cap:* Effectively 0% after 3rd Minor.

### Jackpot Triggers
*   Jackpots (Mini/Minor) can land during **Base Game** (if it transforms to a Bell?) OR explicitly during the **Bonus Game** spins.

## 4. Implementation Requirements
*   **State Machine:** Must track the exact grid state to handle "Sticky" mechanics.
*   **Denomination Awareness:** The engine must know the selected denomination (0.01, 0.10, etc.) to calculate fixed jackpot values.

## 5. Advanced Mechanics (The "Juice")

### A. "Collect" Coin (Respin Teaser)
*   **Symbol:** Gold Coin. Appears ONLY during Paid Respin phase.
*   **Effect:** Pays immediate cash (e.g., 1x Bet).
*   **Goal:** Mitigates the "pain" of paying for respins without hitting the bonus.

### B. Golden Clover (Super Trigger)
*   **Symbol:** Golden Clover (Rare).
*   **Effect:** Acts as a normal Clover Wild.
*   **Super Bonus:** If a Golden Clover is present when the Bonus triggers (5+ Clovers), the Bonus Game upgrades to **SUPER BONUS**.
    *   *Super Perk:* All Cash Bells start with higher values (min 5x).

### C. Mystery Nudge (Second Chance)
*   **Condition:** Player has 4 Clovers (needs 1 more). Respins are over (0 lives).
*   **Trigger:** If a Clover lands just outside the visible area (Row -1 or Row 4) on any reel.
*   **Action:** The reel "Nudges" to bring the Clover into play.
*   **Result:** Bonus Game Triggered! (Huge player satisfaction).

### D. Progressive Bonus (Column Multipliers)
*   **Phase:** Bonus Game (Hold & Win).
*   **Mechanic:** If a vertical Reel (Column) is completely filled with Bells (4 symbols).
*   **Reward:** All Bells in that specific column get a **x2 Multiplier** applied to their values.

### E. Gamble Feature (Risk Game)
*   **Availability:** After any Base Game win (but not after Bonus Game).
*   **Mechanic:** Red or Black card guess.
*   **Math:** Double or Nothing (50/50 chance, 100% RTP for the gamble itself).
*   **Limit:** Max 5 successful gambles in a row.
