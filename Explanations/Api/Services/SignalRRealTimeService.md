# SignalRRealTimeService Explanation

`SignalRRealTimeService.cs` implements `IRealTimeService` using ASP.NET Core SignalR.

## 🛠️ Logic

### `NotifyJackpotUpdate`
- **Scope**: `Clients.All`.
- **Reason**: Everyone in the lobby should see the jackpot ticker increase.

### `NotifyGameUpdate`
- **Scope**: `Clients.User(userId)`.
- **Reason**: Privacy. Only the specific player should see their card being dealt or their slot spin result.

### `NotifyRtpUpdate`
- **Scope**: `Clients.All` (Technically should be restricted to Admin group, but currently broadcasts generally for simplicity).
