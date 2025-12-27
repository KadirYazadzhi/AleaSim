# Bet Entity Explanation

The `Bet` class represents a financial commitment made by a user towards a specific game event. It bridges the user's wallet and the game execution logic.

## 📦 Properties

### Identifiers
- **`Id`** (`Guid`): Unique identifier for the bet transaction.
- **`UserId`** (`Guid`): Direct link to the player who placed the bet.
- **`GameSessionId`** (`Guid`): Links the bet to the specific session where it occurred.
- **`GameRoundId`** (`Guid?`): Nullable link to the executed round.
    - **Why Nullable?** In some game flows, a bet might be placed *before* the round is fully initialized or generated. This allows for flexible state management.

### Financials & Data
- **`Amount`** (`decimal`): The monetary value wagered. This is immediately deducted from `User.Balance` upon creation.
- **`BetData`** (`string`): A JSON payload containing game-specific details.
    - *Example (Roulette)*: `{"type": "red", "amount": 10}`
    - *Example (Slots)*: `{"lines": 20, "betPerLine": 0.1}`
- **`CreatedAt`** (`DateTime`): Exact timestamp of the wager.
