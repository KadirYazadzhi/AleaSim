# Bet & GameRound Configuration - Gameplay Schema

This file configures the `Bet` and `GameRound` entities, which are the highest-volume tables in the system.

## 🛠️ Bet Configuration
- **`HasIndex(b => b.GameRoundId)`**:
    - **Why?** We frequently need to find all bets associated with a specific round to calculate total winnings. Without this index, the database would have to scan the entire table (Full Table Scan), killing performance.
- **`HasPrecision(18, 2)`**:
    - **Currency Handling**: Standard SQL practice for money. `18` digits total, `2` after the decimal point. Matches the C# `decimal` type to prevent rounding errors.

## 🛠️ GameRound Configuration
- **`HasIndex(r => r.GameSessionId)`**:
    - **Why?** Used to load a user's session history.
- **`HasIndex(r => r.ExecutedAt)`**:
    - **Why?** Essential for reporting (e.g., "Daily Revenue Report").
- **Precision**: Both `TotalBetAmount` and `TotalWinAmount` enforce the `(18, 2)` currency standard.
