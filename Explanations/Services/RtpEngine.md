# RtpEngine - Financial Controller

`RtpEngine.cs` contains the logic to ensure the casino remains solvent and fair.

## 🧮 The Logic

### 1. Statistics Aggregation
It keeps three levels of stats in thread-safe dictionaries (`ConcurrentDictionary`):
- **Global**: How is the entire casino doing?
- **Game-Specific**: Is "Slots" paying out too much?
- **User-Specific**: Is "User123" winning impossibly often?

### 2. The Decision (`IsOutcomeAllowed`)
This method simulates a "Look Ahead" check.
- **Target**: `0.95` (95% RTP).
- **Threshold**: `0.05` (5% allowed deviation).
- **Calculation**:
    ```csharp
    projectedRtp = (TotalPaid + potentialWin) / (TotalWagered + currentBet);
    ```
- **Constraint**: `if (projectedRtp > 1.00)` (System is losing money) AND `TotalRounds > 1000` (Sample size is large enough to matter).
    - If both are true, it returns `false`, effectively blocking the win.

## 📉 Statistical Significance
The code includes a check `TotalRounds > 1000`.
- **Why?** In the first 5 rounds, RTP will swing wildly (0% or 500%). We cannot enforce RTP limits on small samples. We only restrict wins once the law of large numbers should have stabilized the math.

