# Game - Configuration Entity

The `Game` class serves as the catalog definition for a specific game type available in the casino platform.

## 🎯 Purpose
To store static configuration and rules that apply to *all* sessions of this game. It allows administrators to enable/disable games or change limits without altering the code.

## 🏗️ Property Breakdown

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `Guid` | Unique ID (e.g., one ID for "European Roulette", another for "Classic Slots"). |
| **`Name`** | `string` | The display name shown in the lobby. |
| **`Description`** | `string` | Rules or marketing text explaining the game. |
| **`MinBet`** | `decimal` | **Risk Management**. Prevents users from betting negligible amounts (e.g., 0.00001) that might spam the system. |
| **`MaxBet`** | `decimal` | **Risk Management**. Limits the maximum liability the casino faces in a single round. |
| **`TargetRTP`** | `double` | **Theoretical Return to Player**. Represented as a fraction (e.g., `0.96` for 96%). This is the mathematical "goal" the game engine tries to achieve over millions of rounds. |
| **`IsActive`** | `bool` | A "Kill Switch". Allows admins to instantly hide a game if a bug is found or during maintenance. |