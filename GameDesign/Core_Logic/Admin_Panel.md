# 🛠️ Admin Panel — AleaSim Design Document

## 📋 Overview

The AleaSim Admin Panel provides operators and developers with secure, real-time visibility and control over the platform. It is a separate SPA (Single Page Application) served from a distinct subdomain (`admin.aleasim.internal`) with its own authentication scope. All actions are fully audited. The panel is never exposed to the public internet — it is accessible only via internal network or VPN.

---

## 🔐 Authentication

### Admin-Only Role

Admin users exist in a **separate user table** (`AdminUsers`) with a higher-privilege JWT scope:

```sql
CREATE TABLE AdminUsers (
    Id          VARCHAR(64) PRIMARY KEY,
    Username    VARCHAR(128) NOT NULL UNIQUE,
    PasswordHash VARCHAR(256) NOT NULL,
    Role        VARCHAR(32)  NOT NULL,  -- "SuperAdmin" | "Admin" | "Moderator" | "Finance"
    MfaSecret   VARCHAR(128),           -- TOTP secret (required)
    CreatedAt   DATETIME     NOT NULL,
    LastLoginAt DATETIME,
    IsActive    BOOL         NOT NULL DEFAULT TRUE
);
```

### Separate Admin JWT Scope

Admin tokens are issued by a dedicated `AdminAuthController` and contain an exclusive scope claim:

```json
{
  "sub": "admin_user_id",
  "scope": "admin:aleasim",
  "role": "SuperAdmin",
  "iss": "https://auth.aleasim.internal",
  "aud": "admin.aleasim.internal",
  "exp": 1738000000,
  "jti": "unique-token-id"
}
```

Player-facing JWTs with `scope: player` are **explicitly rejected** by admin API middleware:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "admin:aleasim");
        policy.RequireRole("SuperAdmin", "Admin", "Moderator", "Finance");
    });
});
```

### MFA Requirement

All admin accounts require **TOTP (Time-based One-Time Password)** MFA. Login flow:

```
1. POST /admin/auth/login  { username, password }
2. Server validates credentials → returns MFA challenge token (60s TTL)
3. POST /admin/auth/mfa    { challengeToken, totpCode }
4. Server validates TOTP → issues admin JWT (4h expiry) + refresh token (24h)
```

Admin sessions expire after **30 minutes of inactivity** (sliding expiry enforced server-side via Redis session store).

### Role Permissions Matrix

| Feature                      | SuperAdmin | Admin | Moderator | Finance |
|------------------------------|-----------|-------|-----------|---------|
| Force Directive (Win/Loss)   | ✅        | ✅    | ❌        | ❌      |
| Shadow Mode Toggle           | ✅        | ❌    | ❌        | ❌      |
| User Account Freeze          | ✅        | ✅    | ❌        | ❌      |
| Manual Balance Adjustment    | ✅        | ❌    | ❌        | ✅      |
| View Financial Reports       | ✅        | ✅    | ❌        | ✅      |
| Chat Moderation              | ✅        | ✅    | ✅        | ❌      |
| System Health View           | ✅        | ✅    | ❌        | ❌      |
| Audit Log View               | ✅        | ✅    | ❌        | ✅      |
| Config Hot-Reload            | ✅        | ❌    | ❌        | ❌      |

---

## 🎯 Force Directive System

Force Directives allow admins to influence the outcome of a specific player's next spin(s). This is used for QA testing, promotional demonstrations, and compliance verification.

### Directive Types

| Directive     | Effect                                                         |
|---------------|----------------------------------------------------------------|
| `force_win`   | Player's next N spins will return a winning outcome            |
| `force_loss`  | Player's next N spins will return a non-winning outcome        |
| `force_bonus` | Player's next spin will trigger the game's bonus round         |
| `clear`       | Removes any active directive for the player                    |

### Storage in Redis

```
Key:   force_directive:{userId}
Type:  Hash
TTL:   600 seconds (10 minutes, auto-expiry)

Fields:
  type        →  "force_win" | "force_loss" | "force_bonus"
  remaining   →  number of spins remaining (e.g., "3")
  set_by      →  adminUserId who issued the directive
  issued_at   →  ISO8601 timestamp
```

### BaseGameEngine Integration

`BaseGameEngine` checks for an active directive **before** outcome resolution:

```csharp
public async Task<SpinResult> ResolveSpinAsync(SpinRequest request)
{
    var directive = await _directiveService.GetActiveDirectiveAsync(request.UserId);

    SpinResult result;
    if (directive is { Type: DirectiveType.ForceWin })
    {
        result = await _outcomeEngine.GenerateForcedWinAsync(request);
        await _directiveService.DecrementRemainingAsync(request.UserId);
    }
    else if (directive is { Type: DirectiveType.ForceLoss })
    {
        result = await _outcomeEngine.GenerateForcedLossAsync(request);
        await _directiveService.DecrementRemainingAsync(request.UserId);
    }
    else
    {
        result = await _outcomeEngine.ResolveNaturalAsync(request);
    }

    await _auditLog.RecordSpinAsync(request, result, directive);
    return result;
}
```

### Issuing a Directive via Admin API

```
POST /admin/users/{userId}/directive
Authorization: Bearer <admin_jwt>

{
  "type": "force_win",
  "spinCount": 3
}
```

Response:

```json
{
  "success": true,
  "directive": {
    "type": "force_win",
    "remaining": 3,
    "expiresAt": "2025-01-15T15:42:00Z",
    "issuedBy": "admin_abc123"
  }
}
```

All directive issuances are written to the Audit Log (see below).

---

## 🕶️ Global Shadow Mode

Shadow Mode is a **SuperAdmin-only** kill switch that enables a special observation mode across the entire platform.

### Behaviour

When Shadow Mode is active:
- All player wins above a configurable threshold (`shadow_mode_win_cap`, default $500) are silently **capped** to the threshold.
- The player UI shows the full (uncapped) win amount.
- The difference is flagged in a separate `ShadowDifferences` ledger for later reconciliation.
- No real funds are transferred for the capped portion until Shadow Mode is disabled and reconciled.
- Jackpots continue to trigger normally but payouts are held in escrow.

> ⚠️ Shadow Mode is intended exclusively for emergency use during suspected fraud investigations or system anomalies. Prolonged use requires compliance team sign-off.

### Toggle

```
POST /admin/system/shadow-mode
Authorization: Bearer <superadmin_jwt>

{ "enabled": true, "reason": "Suspected coordinated bonus abuse - investigation #4421" }
```

Redis flag:

```
SET system:shadow_mode 1         (active)
SET system:shadow_mode 0         (inactive)
SET system:shadow_mode_reason "{reason}"
SET system:shadow_mode_by "{adminId}"
SET system:shadow_mode_at "{timestamp}"
```

The flag is checked by `VaultService` on every credit operation.

---

## 📊 Real-Time Metrics Dashboard

The metrics dashboard streams live data via a dedicated admin SignalR hub (`AdminMetricsHub`), updated every **2 seconds**.

### Dashboard Widgets

| Widget                    | Data Source                             | Granularity  |
|---------------------------|-----------------------------------------|--------------|
| 👥 Active Players         | `SCARD presence:online_set` (Redis)     | Live         |
| 🎰 Spins / Second         | Rolling counter in Redis                | 5s window    |
| 💰 Pool Balance (total)   | Sum of all player balances (Redis cache) | 30s refresh |
| 📈 RTP Drift              | Computed: actual_payout / total_wagered | 1h window    |
| 🏆 Jackpot Pool Values    | `jackpot:pool:*` (Redis)                | Live         |
| 📊 Bets / Second          | Rolling counter in Redis                | 5s window    |
| ⚠️ Error Rate             | Exception counter from telemetry        | 1m window    |
| 🌍 Active Sessions by Region | GeoIP + Redis session set            | 30s refresh  |

### RTP Drift Calculation

```
RTP_drift = (total_won_last_1h / total_wagered_last_1h) × 100

Thresholds:
  Green zone:  88% – 98%
  Yellow zone: 80% – 88% or 98% – 105%
  Red zone:    < 80% or > 105%  → triggers alert email to operations team
```

### Spins Per Second Counter

```
# Incremented by BaseGameEngine on every resolved spin
INCR metrics:spins:counter:{unix_epoch_second}
EXPIRE metrics:spins:counter:{unix_epoch_second} 60

# Read by AdminMetricsHub background service
# Average over last 5 keys for rolling 5-second window
```

---

## 👤 User Management

### User Lookup

```
GET /admin/users/{userId}
GET /admin/users?email={email}
GET /admin/users?username={username}
```

Returns:

```json
{
  "userId": "abc123",
  "username": "JohnD",
  "email": "j***@example.com",
  "registeredAt": "2024-06-01T10:00:00Z",
  "kycStatus": "verified",
  "accountStatus": "active",
  "balances": {
    "real": 150.00,
    "bonus": 25.00
  },
  "activeSession": {
    "sessionId": "sess_xyz",
    "startedAt": "2025-01-15T13:00:00Z",
    "lastActivity": "2025-01-15T14:20:00Z",
    "ipAddress": "192.168.x.x",
    "device": "Chrome 120 / Windows 11"
  },
  "level": 23,
  "totalWagered": 4500.00,
  "totalWon": 4100.00,
  "lifetimeRtp": 91.1
}
```

### Session Management

| Action              | Endpoint                                     | Effect                                                   |
|---------------------|----------------------------------------------|----------------------------------------------------------|
| View active session | `GET /admin/users/{id}/session`              | Returns session details                                  |
| Terminate session   | `DELETE /admin/users/{id}/session`           | Invalidates JWT + SignalR disconnect + Redis session del |
| View session history| `GET /admin/users/{id}/sessions?limit=20`    | Returns last N sessions from SQL                         |

### Account Freeze

Freezing prevents all gameplay, withdrawals, and logins:

```
POST /admin/users/{userId}/freeze
{ "reason": "AML investigation", "durationHours": 72 }
```

Sets Redis flag:
```
SET account:frozen:{userId} "{reason}" EX {durationSeconds}
```

Also writes to SQL `AccountActions` table for compliance records.

### Unfreeze

```
DELETE /admin/users/{userId}/freeze
```

All freeze/unfreeze actions require the `reason` field and are logged to the Audit Log.

---

## 💵 Financial Management

### Manual Balance Adjustment

Admins (Finance role or SuperAdmin) can credit or debit a player's real or bonus balance:

```
POST /admin/users/{userId}/balance/adjust
Authorization: Bearer <admin_jwt>

{
  "wallet":   "real",        // "real" | "bonus"
  "amount":   50.00,         // positive = credit, negative = debit
  "reason":   "Goodwill gesture - support ticket #8821",
  "reference": "TICKET-8821"
}
```

### Safeguards

| Safeguard                    | Detail                                                          |
|------------------------------|-----------------------------------------------------------------|
| Dual-approval for large amounts | Adjustments > $1,000 require a second admin to confirm       |
| Negative balance guard       | System refuses debit if it would push balance below $0          |
| Rate limit                   | Max 10 manual adjustments per admin per hour                    |
| Full audit trail             | Every adjustment written to `WalletTransactions` + Audit Log    |
| Player notification          | Optional: send in-app notification to player about adjustment   |

### Adjustment Flow

```
Admin submits adjustment
        │
        ▼
Validation (amount, wallet type, reason present)
        │
        ▼
If amount > $1,000 → Pending approval queue
        │                   │
        │              Second admin approves
        │                   │
        └───────────────────┘
        │
        ▼
VaultService.AdjustBalanceAsync(userId, amount, type, reason, adminId)
        │
        ├─► Update player wallet in SQL (transaction)
        ├─► Write to WalletTransactions table
        ├─► Write to AuditLog table
        └─► Invalidate balance cache in Redis
```

---

## 🩺 System Health View

### Health Indicators

| Component          | Check Method                                    | Healthy Threshold             |
|--------------------|--------------------------------------------------|-------------------------------|
| 🔴 Redis Primary   | `PING` response < 5ms                           | ✅ < 5ms                      |
| 🔴 Redis Replica   | Replication lag < 100ms                         | ✅ < 100ms                    |
| 🗄️ SQL Primary     | Query `SELECT 1` response < 50ms                | ✅ < 50ms                     |
| 🗄️ SQL Replica     | Replication lag < 500ms                         | ✅ < 500ms                    |
| ⚙️ Game Workers    | Last heartbeat < 30s ago                        | ✅ < 30s                      |
| 📡 SignalR Hub     | Connected clients count, hub exception rate     | ✅ error rate < 0.1%          |
| 💸 Payment Gateway | Connectivity test ping                          | ✅ reachable                  |
| 🧠 BrainService    | Last decision latency < 100ms                   | ✅ < 100ms                    |

### Health API

```
GET /admin/health/full
Authorization: Bearer <admin_jwt>
```

Returns structured JSON per component with status (`healthy`, `degraded`, `unhealthy`) and last checked timestamp. The admin dashboard renders this as a live status grid, polling every **10 seconds**.

### Alert Configuration

Critical health events trigger PagerDuty/Opsgenie alerts:

| Condition                           | Severity  |
|-------------------------------------|-----------|
| Redis primary unreachable            | P1 (page) |
| SQL primary unreachable              | P1 (page) |
| RTP drift in red zone > 5 min       | P2 (alert)|
| Game worker heartbeat missed > 60s  | P2 (alert)|
| Error rate > 5% over 1 min          | P2 (alert)|
| Redis memory > 90% capacity         | P3 (warn) |

---

## 📋 Audit Log

Every admin action is recorded in an immutable audit log. Records are **never deleted** (only soft-read-locked after 7 years for compliance archival).

### Schema

```sql
CREATE TABLE AuditLog (
    Id              BIGINT PRIMARY KEY AUTO_INCREMENT,
    AdminId         VARCHAR(64)     NOT NULL,
    AdminUsername   VARCHAR(128)    NOT NULL,
    Action          VARCHAR(128)    NOT NULL,    -- e.g. "force_directive.set"
    TargetType      VARCHAR(64),                 -- "user" | "system" | "jackpot" | "config"
    TargetId        VARCHAR(64),                 -- e.g. userId or "global"
    Payload         JSON,                        -- full request body (PII-scrubbed)
    IpAddress       VARCHAR(45)     NOT NULL,
    UserAgent       VARCHAR(256),
    ResultStatus    VARCHAR(32)     NOT NULL,    -- "success" | "failure" | "pending_approval"
    ResultMessage   VARCHAR(512),
    CreatedAt       DATETIME        NOT NULL,
    INDEX idx_admin   (AdminId, CreatedAt),
    INDEX idx_target  (TargetType, TargetId),
    INDEX idx_action  (Action),
    INDEX idx_created (CreatedAt)
);
```

### Audit Events Catalogue

| Action String                    | Triggered By                              |
|----------------------------------|-------------------------------------------|
| `user.session.terminate`         | Admin terminates player session           |
| `user.account.freeze`            | Admin freezes account                     |
| `user.account.unfreeze`          | Admin unfreezes account                   |
| `user.balance.adjust`            | Manual balance credit/debit               |
| `force_directive.set`            | Force Win/Loss directive issued           |
| `force_directive.clear`          | Directive manually cleared                |
| `system.shadow_mode.enable`      | Shadow Mode activated                     |
| `system.shadow_mode.disable`     | Shadow Mode deactivated                   |
| `jackpot.pool.manual_seed`       | Admin manually seeds a jackpot pool       |
| `config.hot_reload`              | System config reloaded                    |
| `chat.user.mute`                 | User muted in chat                        |
| `chat.message.delete`            | Chat message deleted                      |
| `admin.login.success`            | Admin successfully authenticated          |
| `admin.login.failure`            | Failed admin login attempt                |
| `admin.mfa.failure`              | Failed TOTP verification                  |

### Audit Log API

```
GET /admin/audit?adminId={id}&action={action}&from={iso}&to={iso}&page=1&limit=50
Authorization: Bearer <admin_jwt>
```

Supports filtering by admin, action type, target user, and date range. Results are paginated (cursor-based on `Id`).

### Immutability

The `AuditLog` table has a database trigger that prevents `UPDATE` and `DELETE` on all rows (enforced at the DB engine level, not just application level). Attempts to modify records generate an additional audit entry.
