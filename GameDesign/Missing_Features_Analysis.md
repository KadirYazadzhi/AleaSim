# 🛑 Missing Features & Implementation Gaps Analysis

This document outlines the discrepancies between the Design Specifications (in `@GameDesign/**`) and the current Codebase implementation.

## 1. Game Mechanics: Clover Chase (Slot)

### ❌ Special Bells (Mini / Minor Jackpots)
*   **Design:** In the Bonus Game (Hold & Win), bells should have a chance to be "Mini" or "Minor" jackpots with fixed values based on denomination. Probability should decrease exponentially with each existing special bell.
*   **Current State:** `SlotGameEngine.PlayBonusRound` generates generic random multipliers (1x - 20x). There is no logic for Mini/Minor types or probability decay.

### ❌ Advanced Mechanics ("The Juice")
*   **Design:**
    *   **Mystery Nudge:** If a 5th Clover lands just outside the visible area (Row -1 or 4), the reel should nudge to trigger the bonus.
    *   **Golden Clover:** A special symbol that upgrades the Bonus Game multipliers.
    *   **Collect Coin:** A symbol appearing during Paid Respins that pays an immediate cash prize to mitigate the cost of respins.
*   **Current State:** None of these mechanics exist in `SlotGameEngine`.

### ❌ Gamble Feature
*   **Design:** A "Double or Nothing" (Red/Black) card game available after any base game win.
*   **Current State:** `SlotGameStateUI` has a `CanGamble` property, but the backend logic (`ProcessAction` in `SlotGameEngine`) is empty/dummy.

### ❌ Denomination Logic
*   **Design:** The game should operate on Credits (e.g., 100 credits * 0.01 BGN denom). Jackpots are fixed to denomination.
*   **Current State:** The engine operates purely on raw Currency (`decimal amount`).

---

## 2. The Brain (Intelligence)

### ❌ Smart Near Misses
*   **Design:** The Brain should analyze the player's "Favorite Symbol" and force a Near Miss specifically with that symbol during cooling phases.
*   **Current State:** `BrainService` has a placeholder for `PreferredNearMissSymbol`, but `SlotGameEngine` does not use it to construct the grid.

### ❌ Flow State (Dynamic Difficulty)
*   **Design:** Adjust volatility based on `AvgSpinInterval`. Fast play (< 2.5s) should trigger higher volatility.
*   **Current State:** `BrainService` calculates `AvgSpinInterval`, but the `SlotGameEngine` does not receive or act upon a volatility modifier from this metric.

---

## 3. The Vault (Finance)

### ❌ Shadow Wallets (Strict pRTP)
*   **Design:** Each user has a "Shadow Wallet" where theoretical RTP is accrued. Wins are paid *from* this personal pool first.
*   **Current State:** `VaultService` checks the global `Game.PoolBalance`. There is no dedicated `ShadowWallet` entity or logic tracking the user's personal "bankable" win potential strictly.

---

## 4. Promotions & Social

### ❌ Raffle "Re-roll"
*   **Design:** If a raffle winner is offline/inactive at the moment of the draw, the system should immediately re-roll.
*   **Current State:** `RaffleBackgroundService` picks a winner but does not implement a retry loop if the user is inactive.

### ❌ Live Leaderboards (Redis)
*   **Design:** High-performance sorted sets in Redis.
*   **Current State:** Implemented in-memory (`ConcurrentDictionary`) within `LeaderboardService`. This is fine for a prototype but not production-ready or scalable.

---

## Summary of Critical Next Steps

1.  **Refactor `SlotGameEngine`** to include:
    *   `BellType` logic (Cash, Mini, Minor).
    *   `Mystery Nudge` evaluation logic.
    *   `Gamble` action processing.
2.  **Update `BrainService`** to enforce:
    *   Specific symbol requests for Near Misses.
    *   Volatility parameters passed to the Engine.
3.  **Enhance `VaultService`** to implement:
    *   True per-user Shadow Wallet accounting (likely requiring a new DB table or column).

---

## 5. Architectural & UX Enhancements (Future Proofing)

### ❌ Hot State Caching (Redis)
*   **Problem:** Currently, every spin reads/writes the full session state to MySQL. This is a massive I/O bottleneck.
*   **Solution:** Move active session state (Grid, Sticky Symbols, Lives) to Redis. Persist to MySQL only on financial transactions (Bet/Win) and Session End.

### ❌ Async Brain (Outcome Queue)
*   **Problem:** The Brain calculates decisions synchronously during the spin request. Complex AI models would cause lag.
*   **Solution:** Implement a background worker that pre-calculates a buffer of decisions (e.g., 5 spins ahead) for each active user. The Spin request simply pops the next decision instantly.

### ❌ Visual "Anticipation" Logic
*   **Problem:** Reels stop instantly or with fixed timing.
*   **Solution:** The Frontend needs logic to detect "Potential Bonus". If Reels 1, 2, and 3 land Clovers, Reels 4 and 5 must spin longer with tension audio/visuals to build excitement.

### ❌ State Recovery
*   **Problem:** Refreshing the browser during a Bonus Round might reset the visual state (though backend is safe).
*   **Solution:** Implement robust `OnInitialized` logic in Blazor to fetch the full `GameState` and visually reconstruct the exact grid/lives before allowing the user to spin again.

### ❌ Multi-Tier Jackpots (Card Suits)
*   **Problem:** Single Global Jackpot reduces variety.
*   **Solution:** Implement a 4-tier progressive system (Clubs, Diamonds, Hearts, Spades) with different drop frequencies and values.