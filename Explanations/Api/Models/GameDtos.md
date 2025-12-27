# GameDtos Explanation

Data Transfer Objects (DTOs) for gameplay.

## 📦 Models

### Session
- **`StartSessionRequest`**: Minimal info needed.
- **`StartSessionResponse`**: Returns the new `SessionId`.

### Betting
- **`PlaceBetRequest`**:
    - `Amount`: The wager.
    - `BetData` (`object`): Flexible payload (e.g., `{"color": "red"}`).
- **`PlaceBetResponse`**:
    - `Result` (`object`): The outcome (e.g., `[1, 2, 3]`).
    - `TotalWin`: The payout.

### Action
- **`GameActionRequest`**: `Action` ("hit"), `ActionData` (optional).
- **`GameActionResponse`**: Returns the new game state (e.g., new hand cards).
