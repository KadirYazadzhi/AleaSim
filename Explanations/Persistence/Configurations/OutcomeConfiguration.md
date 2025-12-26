# OutcomeConfiguration - Result Schema

Configures the `Outcome` entity.

## 🛠️ Schema Rules

- **`HasIndex(o => o.GameRoundId)`**:
    - **Why?** Outcomes are almost always fetched *with* their parent Round. This Foreign Key index speeds up `JOIN` operations.
- **Precision**: `WinAmount` uses standard `(18, 2)` currency format.

## 💡 Design Note
While `GameRound` contains `TotalWinAmount`, `Outcome` contains the specific details. Separating them allows the `GameRound` table to be a lightweight summary for analytics, while `Outcome` (which contains heavy JSON blobs) is only loaded when the user asks for "Details".
