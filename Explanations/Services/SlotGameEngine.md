# SlotGameEngine Implementation Explanation

`SlotGameEngine.cs` simulates a 3-reel slot machine.

## 🎰 Logic

### 1. Reel Configuration
- Defines `_reelStrips` (static arrays of symbol IDs).
- **Probability**: The odds are determined by how many times a symbol appears on the strip.

### 2. Resolution
- **Stops**: Generates 3 random indices.
- **Mapping**: Converts indices to Symbol IDs.
- **Win Check**: `if (s1 == s2 == s3)`. Checks `_paytable` for multiplier.

### 3. Jackpot
- Slots are the primary driver for jackpots. It calls `CheckJackpotTrigger`.
- If triggered, the jackpot amount is added to the line win.

### 4. RTP & Transaction
- Like other engines, it wraps everything in a transaction.
- If RTP rejects the win, it zeros out the `WinAmount` but currently leaves the symbols as a winning combo (Simulation limitation noted in code).
- Notifies frontend with the symbol array (`[1, 1, 1]`) so the client can animate the reels stopping.