# RTPStatistics - Analytics Entity

The `RTPStatistics` class acts as a live scoreboard for the system's mathematical performance.

## 🎯 Purpose
To monitor **Return To Player (RTP)**.
- **Math**: `RTP = Total Paid Out / Total Wagered`
- If a game has an RTP of 96%, it means for every $100 bet, the casino keeps $4 and returns $96 to players (on average, over the long term).
- This entity aggregates these numbers to ensure the games are not "too generous" (losing money) or "too stingy" (cheating players).

## 🏗️ Property Breakdown

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `Guid` | Unique ID. |
| **`GameId`** | `Guid?` | If set, these stats apply to a specific game (e.g., "How is Roulette performing?"). |
| **`UserId`** | `Guid?` | If set, these stats apply to a specific user (e.g., "Is User X winning suspiciously often?"). |
| **`TotalWagered`** | `decimal` | The sum of all bets placed. |
| **`TotalPaid`** | `decimal` | The sum of all winnings paid out. |
| **`CurrentRTP`** | `double` | **Calculated Property**. <br>Formula: `TotalPaid / TotalWagered`. <br>Returns a value like `0.95` (95%). Handles division by zero safely. |
| **`TotalRounds`** | `long` | The sample size. <br>RTP is only statistically significant over large sample sizes (thousands of rounds). High deviation in low `TotalRounds` is normal volatility. |
| **`LastCalculated`** | `DateTime` | When this snapshot was updated. |
