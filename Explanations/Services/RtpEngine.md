# RtpEngine Implementation Explanation

`RtpEngine.cs` serves as the financial "Governor" of the system.

## 🛡️ Logic

### `ProcessWin`
- **Goal**: Decide if a win is allowed *before* paying it.
- **Calculation**:
    `Projected RTP = (TotalPaid + Win) / TotalWagered`
- **Threshold**: Checks if `Projected RTP > Target (0.95) + Deviation (0.05)`.
- **Constraint**: Only enforces this if `TotalRounds > 1000`. This prevents the engine from blocking wins in the early stages when variance is naturally high.

### Real-Time Monitoring
- Calls `_realTimeService.NotifyRtpUpdate` whenever stats change. This allows the Admin Dashboard to show live graphs of game performance.