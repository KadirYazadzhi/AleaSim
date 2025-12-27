# IRealTimeService Interface Explanation

The `IRealTimeService` interface abstracts the push-notification mechanism (SignalR / WebSockets). It allows the backend to send updates to the frontend immediately without the client polling.

## 🛠️ Method Contracts

### `NotifyJackpotUpdate(string name, decimal newValue)`
- **Goal**: Excitement.
- **Usage**: Broadcasts to *all* connected clients that the global jackpot has increased.

### `NotifyGameUpdate(Guid userId, object gameState)`
- **Goal**: Gameplay Feedback.
- **Usage**: Sends specific game state changes (e.g., a card being dealt) only to the specific `userId` playing that game.

### `NotifyRtpUpdate(Guid gameId, double currentRtp)`
- **Goal**: Admin Monitoring.
- **Usage**: Pushes live performance metrics to the Admin Dashboard.
