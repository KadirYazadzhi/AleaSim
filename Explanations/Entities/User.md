# User - Identity Entity

The `User` class represents an actor in the system.

## 🎯 Purpose
To hold identity, authentication (implied), authorization (Role), and financial state (Balance).

## 🏗️ Property Breakdown

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `Guid` | Unique internal identifier. |
| **`Username`** | `string` | Display name. |
| **`Balance`** | `decimal` | **Virtual Wallet**. <br>Stores the user's current funds. <br>• **Crucial**: Modified by `PlaceBet` (subtraction) and `ResolveRound` (addition). <br>• Must be thread-safe in implementation to prevent "Double Spending". |
| **`Role`** | `Role` | Enum (`User`, `Admin`, `Auditor`). Determines what APIs the user can access. |
| **`CreatedAt`** | `DateTime` | Registration timestamp. |
