# Outcome Entity Explanation

The `Outcome` class is a specialized projection of the game result, optimized for client consumption and reporting.

## 📦 Properties

### Core
- **`Id`** (`Guid`): Unique ID.
- **`GameRoundId`** (`Guid`): Links to the authoritative round record.

### Results
- **`ResultJson`** (`string`): A client-friendly JSON payload.
    - Contains detailed win info (e.g., "Line 5 matched", "Dealer busted").
    - Used by the frontend to render animations and messages.
- **`WinAmount`** (`decimal`): The final payout amount.
- **`IsJackpotWin`** (`bool`): Flag indicating if a special jackpot event occurred, triggering specific UI celebrations.
