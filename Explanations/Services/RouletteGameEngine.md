# RouletteGameEngine Implementation Explanation

`RouletteGameEngine.cs` handles the logic for European Roulette.

## 🔄 Workflow

### 1. Betting (`PlaceBet`)
- Accepts a generic `betData` JSON string.
- Deserializes it into a list of `RouletteBet` (e.g., Red, Odd, Number 17).
- Validates the total amount matches the deducted balance.

### 2. Execution (`ResolveRound`)
- **Transaction**: Wraps the entire spin in a database transaction.
- **Spin**: Generates a random number 0-36.
- **Calculation**: Loops through every chip placed:
    - Straight Up: 35:1
    - Color/EvenOdd: 1:1
- **RTP**: Checks `RtpEngine.ProcessWin`.
- **Persistence**: Saves `GameRound` and `Outcome`.
- **Notification**: Pushes the result (Winning Number) to the client via SignalR.

## ⚖️ Math
- **House Edge**: The presence of `0` ensures the house wins on Red/Black/Even/Odd bets when 0 lands, providing the mathematical edge.