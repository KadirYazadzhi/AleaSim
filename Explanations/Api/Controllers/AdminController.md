# AdminController Explanation

`AdminController.cs` exposes restricted endpoints for system monitoring and configuration.

## 🔒 Security
- **`[Authorize(Roles = "Admin")]`**: Strict RBAC enforcement. Only users with the `Admin` role claim in their JWT can access these APIs.

## 🛠️ Endpoints

### `GET api/admin/audit-logs`
- **Purpose**: Compliance.
- **Function**: Returns the immutable history of system events (`AuditEvent`) from the `IAuditService`.

### `GET api/admin/jackpot/global`
- **Purpose**: Dashboarding.
- **Function**: Returns the current value of the Global Jackpot to verify it's accumulating correctly.

### `GET api/admin/rtp/game/{gameId}`
- **Purpose**: Performance Analysis.
- **Function**: Returns the aggregated `RTPStatistics` for a specific game, allowing admins to see if a game is paying out too much or too little.
