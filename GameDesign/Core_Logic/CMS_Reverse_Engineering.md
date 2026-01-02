# 🎨 CMS: Content & Reverse Engineering

## Philosophy
The CMS (Content Management System) is the "Game Engine" in the traditional sense, but with a twist. Instead of generating random numbers and calculating the result, it **manufactures a result** to fit a request.

It is "Dumb" because it doesn't know *why* a win is needed. It just finds the symbols to make it happen.

---

## The Reverse Engineering Process

### 1. The Request
The Brain sends: `TargetWin: $20.00` (on a $1.00 bet).

### 2. The Search (Lookup)
The CMS loads the **Paytable** for the active game (e.g., "Fruit Slot").

*   *Paytable Entry A:* 3 Cherries = 5x
*   *Paytable Entry B:* 3 Oranges = 10x
*   *Paytable Entry C:* 3 Plums = 20x
*   *Paytable Entry D:* 3 7s = 100x

**Match Found:** Entry C (Plums) pays exactly 20x.

### 3. The Construction (Reel Mapping)
Now the CMS must find the physical positions on the virtual reels that display 3 Plums.

*   *Reel 1:* Plum is at Index 4.
*   *Reel 2:* Plum is at Index 12.
*   *Reel 3:* Plum is at Index 20.

**Output:** `Stops: [4, 12, 20]`.

### 4. Handling Imperfect Matches (Fuzzing)
What if The Brain asks for `17x`, but no symbol pays exactly 17x?

**Strategy A: Approximation**
*   Find closest LOWER match (e.g., 15x).
*   Add the remainder (2x) to the user's "Hidden Bank" (to be awarded later) or upgrade the win to the next tier (20x) if the Vault allows.

**Strategy B: Multi-Line Construction**
*   Construct a visual where Line 1 pays 10x and Line 2 pays 7x. (Complex, requires advanced solving).

---

## Special Request: "The Near Miss"
The Brain often requests a **Near Miss** (Teaser) to excite a player during "Cooling" phases.

*   **Request:** `Outcome: Loss`, `VisualIntensity: High`.
*   **CMS Logic:**
    1.  Select a high-value symbol (e.g., Jackpot 7).
    2.  Place it on Reel 1 and Reel 2.
    3.  Deliberately place a *different* symbol on Reel 3, exactly one position away from the winning line.
*   **Result:** Player sees "7 - 7 - Lemon". Heart rate increases. No money paid.

---

## Responsibility
The CMS is the only component that knows about:
*   Reel Strips / Wheel Sectors.
*   Paylines.
*   Bonus Round Mechanics.
*   Asset IDs (Images/Sounds).
