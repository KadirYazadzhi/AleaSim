# Outcome - Result Transfer Entity

The `Outcome` class serves as a Data Transfer Object (DTO) wrapper for the result of a round, optimized for the client/UI.

## 🎯 Purpose
While `GameRound` stores the raw data for the database/audit, `Outcome` formats that data for the player. It answers the immediate question: "Did I win, and how much?"

## 🏗️ Property Breakdown

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `Guid` | Unique ID for the outcome report. |
| **`GameRoundId`** | `Guid` | Reference to the authoritative `GameRound` record. |
| **`ResultJson`** | `string` | **UI-Ready Data**. Contains exactly what the frontend needs to render the result. <br>• *Slots Example*: `{"Reels": [1, 1, 1], "LineMatches": [1]}`. <br>• *Blackjack Example*: `{"PlayerHand": ["Ah", "Kh"], "DealerHand": ["2c", "5d"], "Status": "Blackjack"}`. |
| **`WinAmount`** | `decimal` | The final payout value. |
| **`IsJackpotWin`** | `bool` | Special flag. Triggers specific UI animations (bells, whistles, confetti) if true. |