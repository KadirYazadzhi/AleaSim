# 🌐 Social & Real-Time System — AleaSim Design Document

## 📋 Overview

AleaSim's social layer is built on ASP.NET Core SignalR with a Redis backplane, enabling real-time bidirectional communication between the server and all connected browser clients. It powers the live winners feed, lobby chat, per-game chat rooms, presence tracking, and jackpot broadcast events. The system is designed to scale horizontally across multiple API nodes with no single point of failure.

---

## 🏗️ SignalR Hub Architecture

AleaSim uses three dedicated SignalR hubs, each with a clearly scoped responsibility:

| Hub           | Route          | Responsibility                                         | Auth Required |
|---------------|----------------|--------------------------------------------------------|---------------|
| `GameHub`     | `/hubs/game`   | Game state updates, jackpot tickers, spin results      | ✅ Yes         |
| `ChatHub`     | `/hubs/chat`   | Global/room/private messaging, moderation events       | ✅ Yes         |
| `WinnersHub`  | `/hubs/winners`| Live winners feed, jackpot win broadcasts              | ❌ No (public) |

### Hub Registration

```csharp
// Program.cs
app.MapHub<GameHub>("/hubs/game");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<WinnersHub>("/hubs/winners");
```

### Connection Options

```csharp
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024;   // 32 KB max inbound message
    options.ClientTimeoutInterval     = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval         = TimeSpan.FromSeconds(15);
    options.EnableDetailedErrors      = app.Environment.IsDevelopment();
})
.AddStackExchangeRedis(redisConnectionString, options =>
{
    options.Configuration.ChannelPrefix = RedisChannel.Literal("aleasim:");
});
```

---

## 🔴 Redis Backplane

The Redis backplane allows **any API node** to publish a SignalR message that is received by clients connected to **any other node** in the cluster.

### Architecture Diagram

```
┌─────────────────────────────────────────────┐
│             Load Balancer (sticky off)       │
└──────────┬────────────────┬─────────────────┘
           │                │
    ┌──────▼──────┐  ┌──────▼──────┐
    │  API Node 1 │  │  API Node 2 │
    │  GameHub    │  │  GameHub    │
    │  ChatHub    │  │  ChatHub    │
    │  WinnersHub │  │  WinnersHub │
    └──────┬──────┘  └──────┬──────┘
           │                │
           └───────┬────────┘
                   │
         ┌─────────▼─────────┐
         │   Redis Backplane  │
         │  (Pub/Sub channel) │
         └───────────────────┘
```

> **Sticky sessions are disabled** — SignalR negotiation uses WebSockets with long-polling fallback, and the Redis backplane ensures cross-node delivery. Clients can reconnect to any node transparently.

### Backplane Channels

| Channel Pattern                           | Publisher             | Purpose                       |
|-------------------------------------------|-----------------------|-------------------------------|
| `aleasim:game:jackpot`                    | JackpotService        | Pool value updates            |
| `aleasim:game:winner:{gameId}`            | GameEngine            | Per-game big win events       |
| `aleasim:chat:global`                     | ChatHub               | Global chat relay             |
| `aleasim:chat:room:{roomId}`              | ChatHub               | Room-specific messages        |
| `aleasim:winners:feed`                    | WinnersService        | Live winners feed entries     |
| `aleasim:presence:{userId}`               | PresenceService       | Online/offline status changes |

---

## 🏅 Live Winners Feed

### Trigger Threshold

A win is added to the live winners feed when:

| Condition                      | Threshold     |
|-------------------------------|---------------|
| Win multiplier                | ≥ 100×        |
| Absolute win amount           | ≥ $50.00      |
| Jackpot win (any tier)        | Always        |

Both conditions (multiplier AND amount) must be met for non-jackpot wins.

### Message Format

```csharp
public record WinnerFeedEntry
{
    public string Id          { get; init; }  // UUID
    public string PlayerAlias { get; init; }  // masked: "J***n"
    public string GameName    { get; init; }  // "Book of Dead"
    public string GameId      { get; init; }  // "book-of-dead"
    public decimal WinAmount  { get; init; }  // 4231.50
    public decimal Multiplier { get; init; }  // 847.3
    public DateTime WonAt     { get; init; }  // UTC
    public string BadgeType   { get; init; }  // "big_win" | "jackpot_mini" | ...
    public string AvatarFrame { get; init; }  // player's current avatar frame slug
}
```

Example JSON broadcast:

```json
{
  "id": "f3a2c1d0-...",
  "playerAlias": "J***n",
  "gameName": "Book of Dead",
  "gameId": "book-of-dead",
  "winAmount": 4231.50,
  "multiplier": 847.3,
  "wonAt": "2025-01-15T14:32:01Z",
  "badgeType": "big_win",
  "avatarFrame": "golden-spinner"
}
```

### Client Rendering

- Feed rendered as a scrolling ticker panel (right sidebar or bottom bar).
- New entries animate in from the top (slide-down transition).
- Maximum **50 entries** kept in the live feed DOM; older entries pruned.
- Entries are color-coded by `badgeType`:

| Badge Type       | Color     | Icon |
|------------------|-----------|------|
| `big_win`        | Gold      | ⭐   |
| `mega_win`       | Purple    | 💜   |
| `jackpot_mini`   | Bronze    | 🥉   |
| `jackpot_minor`  | Silver    | 🥈   |
| `jackpot_major`  | Gold      | 🥇   |
| `jackpot_mega`   | Diamond   | 💎   |

- Players can react to feed entries with 👏 emoji (increments a Redis counter, visible to all).
- Feed is **fully public** — `WinnersHub` requires no authentication.

---

## 💬 Chat System

### Room Types

| Room Type      | Key Pattern              | Access              | Description                          |
|----------------|--------------------------|---------------------|--------------------------------------|
| Global         | `room:global`            | All authenticated   | Platform-wide chat                   |
| Per-Game       | `room:game:{gameId}`     | All authenticated   | Game lobby chat                      |
| VIP Lounge     | `room:vip`               | VIP role only       | Exclusive VIP discussion room        |
| Private (DM)   | `room:dm:{userId1}:{userId2}` | Participants only | Direct messaging between players    |
| Support        | `room:support:{ticketId}`| Player + Staff     | Support conversation thread          |

### Message Schema

```csharp
public record ChatMessage
{
    public string  MessageId   { get; init; }   // UUID
    public string  RoomId      { get; init; }   // e.g. "room:global"
    public string  SenderId    { get; init; }   // userId
    public string  SenderAlias { get; init; }   // display name
    public string  AvatarFrame { get; init; }   // cosmetic frame slug
    public string  PlayerTitle { get; init; }   // e.g. "High Roller"
    public string  Content     { get; init; }   // sanitized text (max 500 chars)
    public DateTime SentAt     { get; init; }   // UTC
    public bool    IsSystem    { get; init; }   // true for system messages
    public string? ReplyToId   { get; init; }   // optional quoted message ID
}
```

### Rate Limiting

| User Role  | Max Messages | Window    |
|------------|-------------|-----------|
| Standard   | 5 messages  | 10 seconds |
| VIP        | 15 messages | 10 seconds |
| Staff      | Unlimited   | —          |

Rate limits enforced via Redis sliding window:

```
Key:   chat:ratelimit:{userId}:{roomId}
Type:  Sorted Set (timestamps as score)
TTL:   10 seconds rolling
```

### Message Persistence

- **Recent messages**: Last 200 messages per room stored in Redis `LIST` (RPUSH + LTRIM).
- **Full history**: All messages written asynchronously to SQL `ChatMessages` table (non-blocking).
- On join, the client receives the last 50 messages (hydrated from Redis).

```sql
CREATE TABLE ChatMessages (
    Id          BIGINT PRIMARY KEY AUTO_INCREMENT,
    MessageId   VARCHAR(64)  NOT NULL UNIQUE,
    RoomId      VARCHAR(128) NOT NULL,
    SenderId    VARCHAR(64)  NOT NULL,
    Content     TEXT         NOT NULL,
    SentAt      DATETIME     NOT NULL,
    DeletedAt   DATETIME     NULL,
    DeletedBy   VARCHAR(64)  NULL,
    INDEX idx_room_time (RoomId, SentAt)
);
```

---

## 🛡️ Moderation

### Admin Commands

All moderation actions are issued through `ChatHub` using privileged method calls (requires `Admin` or `Moderator` role):

| Command                   | Method Call                              | Effect                                              |
|---------------------------|------------------------------------------|-----------------------------------------------------|
| Mute user (room)          | `MuteUserInRoom(userId, roomId, minutes)` | Blocks sends to that room for N minutes            |
| Mute user (global)        | `MuteUserGlobal(userId, minutes)`         | Blocks all chat sends                              |
| Ban user from chat        | `BanUserFromChat(userId, reason)`         | Permanent mute, logged to audit trail              |
| Delete message            | `DeleteMessage(messageId, reason)`        | Soft-delete in DB, remove from Redis list, broadcast removal |
| Slow mode (room)          | `SetSlowMode(roomId, delaySeconds)`       | Forces per-user delay between messages in room     |
| Clear room                | `ClearRoom(roomId)`                       | Clears Redis list, marks all messages deleted in DB |
| Broadcast system message  | `SendSystemMessage(roomId, content)`      | Posts a styled system notice to a room             |

### Auto-Filter

A content filter pipeline runs synchronously before any message is persisted or broadcast:

```
Raw Input
    │
    ▼
1. Strip HTML / XSS sanitization (HtmlEncoder)
    │
    ▼
2. Profanity filter (configurable word list, Redis-cached)
    │
    ├── Match found → replace with *** or block (config per severity)
    │
    ▼
3. URL/link filter
    │
    ├── Unwhitelisted URLs → strip or block
    │
    ▼
4. Spam detection (repeated chars, all-caps threshold)
    │
    ▼
Sanitized Content (or rejected with error code)
```

### Mute Storage

```
# Room-scoped mute
SET chat:mute:{userId}:room:{roomId} 1 EX {seconds}

# Global mute
SET chat:mute:{userId}:global 1 EX {seconds}

# Permanent ban flag
SET chat:ban:{userId} {reason}  (no expiry)
```

---

## 👥 Online Presence Tracking

### Mechanism

On WebSocket connection/disconnection, `PresenceService` maintains player online state:

```csharp
// On connect
await _redis.StringSetAsync($"presence:{userId}", "online", TimeSpan.FromMinutes(2));
await _redis.SetAddAsync("presence:online_set", userId);

// Heartbeat (client sends ping every 30s)
await _redis.StringSetAsync($"presence:{userId}", "online", TimeSpan.FromMinutes(2));

// On disconnect
await _redis.KeyDeleteAsync($"presence:{userId}");
await _redis.SetRemoveAsync("presence:online_set", userId);
```

### Online Count

```
# Total online players
SCARD presence:online_set

# Broadcast online count via GameHub every 10 seconds
```

### Friend Presence

For friends list, presence is exposed via the Profile API endpoint rather than WebSocket push (to avoid flooding users with friend status events). Clients poll `/api/presence/friends` every 30 seconds.

---

## 🔄 Connection Lifecycle

### Reconnection Logic

The JavaScript SignalR client is configured with automatic reconnection:

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/chat", {
        accessTokenFactory: () => getAuthToken()
    })
    .withAutomaticReconnect([0, 1000, 3000, 10000, 30000])  // ms delay sequence
    .configureLogging(signalR.LogLevel.Warning)
    .build();

connection.onreconnecting(error => {
    showConnectionBanner("Reconnecting…", "warning");
});

connection.onreconnected(connectionId => {
    hideConnectionBanner();
    rejoinRooms();        // re-subscribe to all previously joined rooms
    fetchMissedMessages(); // fetch messages since last known message ID
});

connection.onclose(error => {
    showConnectionBanner("Disconnected. Refresh to reconnect.", "error");
});
```

### Session Restoration

On reconnect, the client sends its **last known message ID** per room:

```json
{ "event": "restore_session", "rooms": { "room:global": "msg_uuid_last" } }
```

The server responds with any missed messages (max last 50) for each room.

### Server-Side Connection Tracking

```csharp
public override async Task OnConnectedAsync()
{
    var userId = Context.UserIdentifier;
    await _redis.SetAddAsync($"hub:connections:{userId}", Context.ConnectionId);
    await _presenceService.SetOnlineAsync(userId);
    await base.OnConnectedAsync();
}

public override async Task OnDisconnectedAsync(Exception? exception)
{
    var userId = Context.UserIdentifier;
    await _redis.SetRemoveAsync($"hub:connections:{userId}", Context.ConnectionId);

    // Only mark offline if no other connections remain
    var remaining = await _redis.SetLengthAsync($"hub:connections:{userId}");
    if (remaining == 0)
        await _presenceService.SetOfflineAsync(userId);

    await base.OnDisconnectedAsync(exception);
}
```

This supports players with **multiple tabs/devices** — presence is only cleared when all connections drop.

---

## ⚡ Performance Considerations

### Message Rate Limiting (Server-Side)

Beyond per-user chat limits, the system applies global backpressure:

| Metric                     | Limit                 | Action on Exceed          |
|----------------------------|-----------------------|---------------------------|
| Winners feed entries/sec   | 10 per second         | Queue and batch           |
| Chat messages/sec (global) | 200/sec system-wide   | Drop oldest pending       |
| SignalR group size         | 50,000 per group      | Shard by region           |
| Redis backplane msg/sec    | 5,000/sec             | Monitor; scale Redis if needed |

### Pagination

Chat history beyond the initial 50 messages is loaded on scroll-up (infinite scroll):

```
GET /api/chat/{roomId}/history?before={messageId}&limit=50
```

The server queries SQL `ChatMessages` with cursor-based pagination (no OFFSET) for O(log n) performance.

### Message Compression

SignalR messages above 1KB are compressed using `MessagePack` serialization instead of JSON:

```csharp
builder.Services.AddSignalR()
    .AddMessagePackProtocol();
```

This reduces payload size by ~60% for typical chat messages, reducing Redis backplane bandwidth.

### Connection Limits

| Tier         | Max Concurrent Connections per Node |
|--------------|-------------------------------------|
| Development  | 1,000                               |
| Staging      | 10,000                              |
| Production   | 100,000 (with Azure SignalR Service) |

For production scale beyond 100k concurrent users, Azure SignalR Service replaces the self-hosted hub, with the Redis backplane replaced by Azure's managed relay infrastructure.
