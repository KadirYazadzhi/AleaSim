# Role Enum Explanation

The `Role` enum defines the authorization levels within the system, underpinning the Role-Based Access Control (RBAC).

## 📋 Values

### `User`
- **Access Level**: Standard.
- **Capabilities**: Can play games, deposit/withdraw, view own history.

### `Admin`
- **Access Level**: Elevated.
- **Capabilities**: Can configure games, manage users, view system-wide analytics.

### `Auditor`
- **Access Level**: Read-Only / Compliance.
- **Capabilities**: Can view `AuditLogs`, verify RNG seeds, and inspect game history. Cannot modify data or play games.