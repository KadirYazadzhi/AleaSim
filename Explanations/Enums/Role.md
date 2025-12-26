# Role Enum - Authorization Levels

The `Role` enum defines the hierarchy of permissions within the application.

## 🎯 Purpose
To implement Role-Based Access Control (RBAC). It ensures that a standard player cannot trigger administrative functions (like resetting a Jackpot) and an auditor can see data but not play.

## 📋 Values

### `User` (0)
- **Description**: The standard customer.
- **Permissions**:
    - Can Start Sessions.
    - Can Place Bets.
    - Can View *Own* History.
    - Cannot view other users' data or change game configurations.

### `Admin` (1)
- **Description**: The system manager.
- **Permissions**:
    - Can Configure Games (Change RTP, Limits).
    - Can Manage Users (Ban, Refund).
    - Can View System-wide Analytics.

### `Auditor` (2)
- **Description**: A regulatory observer (Third-party).
- **Permissions**:
    - **Read-Only** access to everything.
    - Specifically focuses on `IAuditService` logs.
    - Cannot modify data, place bets, or change configurations.
    - Used to verify `DeterministicRngService` fairness.
