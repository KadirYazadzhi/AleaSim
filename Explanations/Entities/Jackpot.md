# Jackpot - Accumulator Entity

The `Jackpot` class represents a progressive prize pool that grows with every bet placed.

## 🎯 Purpose
To manage the state of high-value prizes. Jackpots are "Progressive", meaning a small percentage of every bet is "taxed" and added to this pot.

## 🏗️ Property Breakdown

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `Guid` | Unique ID. |
| **`Name`** | `string` | Display name (e.g., "Mega Moolah", "Mini Jackpot"). |
| **`CurrentValue`** | `decimal` | The actual amount of money currently in the pot. This changes in real-time as bets are placed. |
| **`ContributionRate`** | `decimal` | The configuration for growth. <br>• E.g., `0.01` (1%). <br>• If a user bets $100, $1 is added to `CurrentValue`. |
| **`IsGlobal`** | `bool` | **Scope Definition**. <br>• `true`: Shared across ALL games. Everyone contributes, anyone can win. <br>• `false`: Specific to one game type (e.g., only Slot players contribute). |
| **`GameId`** | `Guid?` | Nullable. If `IsGlobal` is false, this links to the specific `Game` that owns this jackpot. |
| **`LastUpdated`** | `DateTime` | Concurrency tracking. Helps identifying when the value last changed. |