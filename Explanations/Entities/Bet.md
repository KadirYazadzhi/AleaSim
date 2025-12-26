# Bet - Wager Entity

The `Bet` class represents a financial commitment made by a player on a specific outcome of a game.

## 🎯 Purpose
To track money deducted from a user's wallet *before* a game result is determined. It separates the "intent to play" from the "result of play".

## 🏗️ Property Breakdown

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `Guid` | Unique identifier for this specific bet transaction. |
| **`GameRoundId`** | `Guid` | **Foreign Key**. Connects this bet to a specific `GameRound`. This is critical for auditing: knowing exactly which spin of the wheel or deal of cards this money was wagered on. |
| **`Amount`** | `decimal` | The monetary value. `decimal` is **mandatory** for financial calculations (instead of `double` or `float`) to avoid floating-point rounding errors. |
| **`BetData`** | `string` | Stores game-specific wagering details. <br>• **Roulette**: `[{"Type":"Red","Amount":10}, {"Type":"Number","Value":"17","Amount":5}]` <br>• **Slots**: `{"Lines": 20, "BetPerLine": 0.5}` <br>• **Blackjack**: Usually empty or side-bet details. |
| **`CreatedAt`** | `DateTime` | When the bet was accepted by the server. |

## 🔗 Relationships
- **1 GameRound** has **1 or Many Bets** (e.g., in Roulette, a player can place chips on Red, Odd, and Number 7 simultaneously).