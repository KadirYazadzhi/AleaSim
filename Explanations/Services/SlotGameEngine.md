# SlotGameEngine - 3-Reel Slot Implementation

`SlotGameEngine.cs` simulates a classic mechanical 3-reel slot machine.

## 🎰 Mechanics

### 1. Reel Strips
Defined as arrays of integers:
```csharp
new[] { 1, 2, 3, 4, 5, 1, 2, 3 }
```
- Represents the physical strip of symbols.
- **Probabilities**: The frequency of a number (e.g., `1` appears twice, `5` appears once) determines the odds of landing it.

### 2. The Spin (`ResolveRound`)
- Generates 3 random numbers (Stops), one for each reel.
- **Determinisim**: Uses `HashCode.Combine(roundNumber, i)` to ensure Reel 1, 2, and 3 get distinct but reproducible random numbers.
- Maps the "Stop Index" to the "Symbol".

### 3. Paytable & Win Logic
- **Simple Logic**: Only checks for "3 of a Kind".
- `if (Reel1 == Reel2 == Reel3)` -> Win.
- **Multipliers**:
    - Symbol 1: 10x Bet
    - Symbol 5: 0.5x Bet
- **Jackpot**: Slots are the primary driver for Jackpots. It calls `JackpotService.CheckJackpotTrigger` *in addition* to the line win check.

### 4. RTP Intervention
- Even if the symbols line up (e.g., 3x Symbol 1), the `RtpEngine` is consulted.
- If the engine says "No", the `winAmount` is set to 0.
- *Visual Discrepancy Note*: In a real game, if the backend forces a loss, the frontend MUST NOT show a winning combination of symbols. The backend would typically re-roll the stops to show a losing combo. This simulation simplifies that step.
