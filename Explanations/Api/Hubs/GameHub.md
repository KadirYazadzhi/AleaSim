# GameHub Explanation

`GameHub.cs` is a SignalR Hub that manages real-time websocket connections.

## 🎯 Purpose
To allow the server to push updates to the client (Server-Sent Events) instead of the client asking "Did I win yet?" every second.

## 🛠️ Methods
- **`JoinGame(string gameType)`**: Adds the connection to a specific group (e.g., "RoulettePlayers"). This allows broadcasting messages to all players of a specific game.
- **`LeaveGame`**: Clean up.
