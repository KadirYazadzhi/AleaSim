# 🧙 RPG Quest System — AleaSim Design Document

## 📋 Overview

The RPG Quest System transforms standard casino gameplay into an engaging progression loop. Players earn XP by completing quests tied to real game actions, unlocking rewards, titles, and cosmetic enhancements as they level up. The system is designed to be non-intrusive to core game logic — `QuestService` listens to events emitted by `BaseGameEngine` without altering game outcomes.

---

## 🌟 XP System & Level Thresholds

### Formula

XP required to reach level `N` from level `N-1`:

```
XP(N) = floor(100 * (N - 1)^1.75) + 100
```

This produces a gentle early curve that steepens significantly at higher levels, rewarding long-term players.

### Level Thresholds Table (Levels 1–100)

| Level | XP to Next Level | Cumulative XP | Title Unlocked         |
|-------|-----------------|---------------|------------------------|
| 1     | 100             | 0             | Newcomer               |
| 2     | 233             | 100           | —                      |
| 3     | 393             | 333           | —                      |
| 4     | 575             | 726           | —                      |
| 5     | 777             | 1,301         | Apprentice             |
| 10    | 1,900           | 5,988         | Gambler                |
| 15    | 3,327           | 15,340        | Risk Taker             |
| 20    | 5,028           | 30,250        | High Roller            |
| 25    | 6,975           | 51,700        | Sharp                  |
| 30    | 9,146           | 80,500        | Veteran                |
| 40    | 14,310          | 157,100       | Ace                    |
| 50    | 20,481          | 272,800       | Legend                 |
| 60    | 27,600          | 436,600       | Mythic                 |
| 75    | 41,200          | 775,000       | Immortal               |
| 90    | 57,500          | 1,230,000     | Celestial              |
| 100   | —               | 1,750,000     | 👑 Grand Master        |

> Levels 1–9 are fast to clear (~1–2 hours of play). Levels 90–100 require weeks of consistent play.

### XP Multipliers

| Condition                        | Multiplier |
|----------------------------------|------------|
| First session of the day         | ×1.5       |
| Active VIP membership            | ×2.0       |
| Weekend bonus event              | ×1.25      |
| Referral active in last 7 days   | ×1.1       |
| Streak (7+ consecutive days)     | ×1.75      |

---

## 📜 Quest Types

### 1. 🗓️ Daily Quests

- Refreshed at **00:00 UTC** each day.
- Players are assigned **3 random daily quests** from an active pool.
- Completion window: 24 hours.
- Incomplete dailies **do not roll over**.

### 2. 📅 Weekly Quests

- Refreshed every **Monday 00:00 UTC**.
- Players receive **2 weekly quests**, typically longer milestones.
- Offer higher XP and may include Bonus Balance rewards.

### 3. 🏆 Achievement Quests

- Permanent quests that can only be completed **once per account**.
- Tracked cumulatively across the entire account lifetime.
- Unlocked progressively (later achievements require earlier ones).

### 4. 🕵️ Hidden Quests

- Not visible in the quest log until triggered.
- Triggered by rare or unusual player actions (e.g., winning 5 times on a single session, playing at 3:00 AM).
- Reward cosmetic exclusives (Avatar Frames, titles).
- Emit a discovery notification on reveal.

---

## 🗂️ Quest Categories

### Spin-Based

| Quest Name              | Requirement                        | XP Reward |
|-------------------------|------------------------------------|-----------|
| Warm Up                 | Spin 10 times                      | 50        |
| Reel Runner             | Spin 100 times in one day          | 200       |
| Marathon Spinner        | Spin 1,000 times (lifetime)        | 1,500     |
| Turbo Mode              | Spin 50 times within 30 minutes    | 300       |

### Win-Based

| Quest Name              | Requirement                        | XP Reward |
|-------------------------|------------------------------------|-----------|
| First Blood             | Win your first bet                 | 100       |
| Lucky Streak            | Win 5 bets in a row                | 400       |
| Big Winner              | Win a single bet ≥ 50x stake       | 750       |
| Centurion               | Win 100 bets (lifetime)            | 2,000     |

### Game-Specific

| Quest Name              | Requirement                                | XP Reward |
|-------------------------|--------------------------------------------|-----------|
| Slots Devotee           | Play 50 rounds on any slot game            | 300       |
| Table Master            | Win 10 rounds of Blackjack                 | 500       |
| Roulette Royale         | Place bets on 20 unique roulette numbers   | 350       |
| Crash Survivor          | Cash out above 5x in Crash               | 600       |

### Social

| Quest Name              | Requirement                                | XP Reward |
|-------------------------|--------------------------------------------|-----------|
| Social Butterfly        | Send 10 chat messages in Global room       | 100       |
| Friend Bringer          | Refer 1 new player who deposits           | 1,000     |
| Community Pillar        | Refer 5 active players                     | 5,000     |
| Cheerleader             | React to 5 win announcements in Winners feed | 150     |

---

## ⚙️ QuestService Architecture

### Event Interception Model

`QuestService` subscribes to events published by `BaseGameEngine` via an internal event bus (MediatR or a lightweight pub/sub channel). It **never** modifies game results — it is purely observational.

```csharp
// BaseGameEngine publishes after every resolved round
public class SpinCompletedEvent : INotification
{
    public string UserId      { get; init; }
    public string GameId      { get; init; }
    public decimal BetAmount  { get; init; }
    public decimal WinAmount  { get; init; }
    public decimal Multiplier { get; init; }
    public DateTime Timestamp { get; init; }
}

// QuestService handler
public class QuestProgressHandler : INotificationHandler<SpinCompletedEvent>
{
    public async Task Handle(SpinCompletedEvent evt, CancellationToken ct)
    {
        await _questService.ProcessEventAsync(evt.UserId, QuestTrigger.Spin, evt);
        if (evt.WinAmount > 0)
            await _questService.ProcessEventAsync(evt.UserId, QuestTrigger.Win, evt);
        if (evt.Multiplier >= 50)
            await _questService.ProcessEventAsync(evt.UserId, QuestTrigger.BigWin, evt);
    }
}
```

### Processing Pipeline

```
SpinCompletedEvent
        │
        ▼
QuestProgressHandler
        │
        ├─► Load active quests for userId (Redis hot cache, 30 min TTL)
        │
        ├─► Match quests by trigger type
        │
        ├─► Increment progress counters (Redis HINCRBY)
        │
        ├─► Check completion thresholds
        │         │
        │         ├── Not complete → Update Redis, return
        │         │
        │         └── Complete → Mark complete in Redis
        │                        │
        │                        ├─► Enqueue RewardGrantJob (background)
        │                        ├─► Persist to SQL (cold store)
        │                        └─► Push WebSocket notification to player
        │
        └─► Periodic flush: Redis → SQL (every 5 min via BackgroundService)
```

---

## 🎁 Reward Types

| Reward Type     | Description                                              | Example                         |
|-----------------|----------------------------------------------------------|---------------------------------|
| **XP**          | Added directly to player XP pool                        | +500 XP                         |
| **Bonus Balance** | Added to player's bonus wallet (wagering requirement applies) | +$5.00 Bonus             |
| **Avatar Frames** | Cosmetic border shown on player profile and chat       | "Golden Spinner" frame          |
| **Perks**        | Temporary gameplay modifiers (XP boost, cashback)       | 24hr ×2 XP booster              |
| **Titles**       | Display name suffix shown in leaderboard/chat           | "Mythic Spinner"                |
| **Free Spins**   | Granted to a specified game                              | 20 free spins on Starburst      |

### Reward Grant Flow

```csharp
public class RewardGrantJob : IBackgroundJob
{
    public async Task ExecuteAsync(RewardPayload payload)
    {
        switch (payload.Type)
        {
            case RewardType.XP:
                await _xpService.AddXpAsync(payload.UserId, payload.Amount);
                break;
            case RewardType.BonusBalance:
                await _walletService.CreditBonusAsync(payload.UserId, payload.Amount);
                break;
            case RewardType.AvatarFrame:
                await _cosmeticService.UnlockFrameAsync(payload.UserId, payload.FrameId);
                break;
            case RewardType.Perk:
                await _perkService.ActivatePerkAsync(payload.UserId, payload.PerkId, payload.DurationHours);
                break;
        }

        await _notificationService.PushAsync(payload.UserId, new QuestCompletedNotification(payload));
    }
}
```

---

## 🗄️ Data Storage Strategy

### Redis (Hot Store)

Used for all real-time, high-frequency operations:

```
# Active quest progress for a user
HSET quest:progress:{userId} quest_{questId}_progress {value}
EXPIRE quest:progress:{userId} 1800   # 30 min rolling TTL

# XP and level
SET player:xp:{userId} {totalXp}
SET player:level:{userId} {level}

# Quest completion flags (prevent double-award)
SET quest:completed:{userId}:{questId} 1 EX 86400
```

### SQL (Cold Store)

Used for permanent record-keeping and audit:

```sql
CREATE TABLE QuestCompletions (
    Id            BIGINT PRIMARY KEY AUTO_INCREMENT,
    UserId        VARCHAR(64)     NOT NULL,
    QuestId       VARCHAR(64)     NOT NULL,
    CompletedAt   DATETIME        NOT NULL,
    RewardType    VARCHAR(32)     NOT NULL,
    RewardValue   DECIMAL(18, 4),
    RewardMeta    JSON,
    INDEX idx_user_quest (UserId, QuestId),
    INDEX idx_completed_at (CompletedAt)
);

CREATE TABLE PlayerXpLedger (
    Id          BIGINT PRIMARY KEY AUTO_INCREMENT,
    UserId      VARCHAR(64)  NOT NULL,
    Delta       INT          NOT NULL,
    Source      VARCHAR(64)  NOT NULL,  -- e.g. "quest:reel_runner"
    TotalAfter  INT          NOT NULL,
    CreatedAt   DATETIME     NOT NULL,
    INDEX idx_user         (UserId),
    INDEX idx_user_created (UserId, CreatedAt)
);
```

### Sync Strategy

A `BackgroundService` runs every **5 minutes**, flushing dirty Redis entries to SQL. On Redis eviction or restart, the system rehydrates from SQL for active players.

---

## 🏅 Leaderboard

### Leaderboard Types

| Leaderboard       | Scope        | Reset Period | Rewards                   |
|-------------------|--------------|--------------|---------------------------|
| All-Time XP       | Global       | Never        | Permanent trophy badge    |
| Weekly XP         | Global       | Monday UTC   | Bonus Balance top 10      |
| Level Ranking     | Global       | Never        | Prestige display          |
| Friends           | Social graph | Never        | Bragging rights           |

### Redis Sorted Set Implementation

```
# All-time XP leaderboard
ZADD leaderboard:xp:alltime {totalXp} {userId}

# Weekly XP leaderboard
ZADD leaderboard:xp:weekly:{isoWeek} {weeklyXp} {userId}
EXPIRE leaderboard:xp:weekly:{isoWeek} 1209600  # 14 days

# Query top 100
ZREVRANGE leaderboard:xp:alltime 0 99 WITHSCORES
```

---

## 👤 Player Profile Page Integration

The player profile page surfaces the following quest/RPG data:

| Section              | Data Source              | Update Frequency  |
|----------------------|--------------------------|-------------------|
| Current Level & XP   | Redis `player:xp:{id}`   | Real-time (WS)    |
| XP progress bar      | Computed from level table | Real-time (WS)   |
| Active Quests        | Redis quest progress      | Real-time (WS)    |
| Completed Quests     | SQL `QuestCompletions`    | On page load      |
| Earned Avatar Frames | SQL cosmetics table       | On page load      |
| Active Perks         | Redis perk store          | Real-time (WS)    |
| Leaderboard Rank     | Redis sorted set          | Every 60 seconds  |
| Titles               | SQL titles table          | On page load      |

### WebSocket Notification Payload

```json
{
  "event": "quest_completed",
  "questId": "reel_runner",
  "questName": "Reel Runner",
  "xpAwarded": 200,
  "totalXp": 4532,
  "newLevel": null,
  "rewardType": "xp",
  "rewardMeta": {}
}
```

If the XP gain triggers a level-up, `newLevel` is populated and a separate `level_up` event fires:

```json
{
  "event": "level_up",
  "previousLevel": 9,
  "newLevel": 10,
  "titleUnlocked": "Gambler",
  "totalXp": 5988
}
```

---

## 🔒 Anti-Abuse Safeguards

- **Rate limiting**: Quest progress events are processed at most once per second per user (debounced in Redis).
- **Idempotency**: Completion flags in Redis prevent double-reward on retry.
- **Audit trail**: All XP grants written to `PlayerXpLedger` with source reference.
- **Admin override**: Admins can revoke quest completions and deduct XP via the Admin Panel audit log.
- **Hidden quest throttle**: Max 2 hidden quests per player per day to prevent farming.
