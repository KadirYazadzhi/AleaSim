# JackpotService - Progressive System Implementation

`JackpotService.cs` handles the logic for the Global and Local jackpot pools.

## ⚙️ Key Mechanisms

### 1. In-Memory Storage
It uses `ConcurrentDictionary<Guid, Jackpot>` to hold the state of local jackpots.
- **Global Jackpot**: Stored in a private field `_globalJackpot`.

### 2. Contribution Logic (`Contribute`)
Called every time a bet is placed.
```csharp
lock (_globalJackpot) {
    _globalJackpot.CurrentValue += betAmount * 0.01; // Adds 1%
}
```
- **Locking**: Crucial. If two users bet at the same time, `+=` is not atomic. Without `lock`, one contribution could overwrite the other.

### 3. Trigger Logic (`CheckJackpotTrigger`)
This determines if a user wins the pot.
- **RNG**: Uses `_rngService.GetNextDouble`.
- **Math**:
    - **Local Win**: `roll < 0.0001` (1 in 10,000 chance).
    - **Global Win**: `roll < 0.00001` (1 in 100,000 chance).
- **Payout & Reset**:
    - If won, it grabs the `CurrentValue`.
    - **Immediately** resets `CurrentValue` to the seed amount (e.g., 500 or 10,000). This prevents the casino from having an empty pot ($0) after a win.

## ⚠️ Simulation Note
In a real distributed system (multiple servers), `lock` only works on *one* server. A real implementation would use **Redis** (atomic INCR) or **Database Transactions** to handle jackpot state across a cluster.
