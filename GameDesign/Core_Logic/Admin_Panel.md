# 🛠️ Admin Panel — AleaSim Design Document (v2.0 - Integrated)

## 📋 Overview

The AleaSim Admin Panel (Backoffice) provides operators with secure, real-time visibility and control. Unlike earlier iterations, the panel is now **fully integrated into the main Blazor SPA**, protected by server-side role-based policies and a dedicated `AdminLayout`. It is fully responsive, allowing for professional platform management from any device.

---

## 🔐 Authentication & Security

### Unified Identity Model
Admins share the main `Users` table but possess the `Role.Admin` flag. This simplifies session management while maintaining strict isolation through middleware.

```csharp
// Security Policy in Program.cs
builder.Services.AddAuthorization(options => {
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});
```

### Access Control
*   **Route Protection:** Pages under `/admin/` and the `/dashboard` route require the `Admin` role.
*   **UI Hardening:** Sensitive controls (Force Directives, Balance Injection) are only rendered if the user's JWT contains the administrative claim.
*   **Rate Limiting:** Admin actions are subject to specific throttling to prevent automated session hijacking or brute-force override attempts.

---

## 🎯 Control Systems

### 1. Force Directive System
Allows real-time override of the AI Brain for QA or VIP management.
*   **Force Win/Loss:** Directs the next N spins to a specific outcome.
*   **Force Bonus:** Guarantees a bonus game trigger on the next round.
*   **Expiry:** All directives expire after 10 minutes to prevent permanent account bias.

### 2. Platform Sentinel (Live Monitoring)
A real-time SignalR stream of every system event:
*   **Event Feed:** Visualizes every bet, win, and decision type (Random vs Hook).
*   **Big Win Broadcast:** Platform-wide alerts for high-multiplier wins.
*   **Audit Chain:** Displays the live cryptographic hash link between audit events.

### 3. Simulation Center (The Lab)
High-performance mathematical validation:
*   **Headless Simulation:** Run up to 10,000 iterations in seconds via the UI.
*   **RTP Analysis:** Live calculation of Actual vs. Theoretical RTP.
*   **Decision Distribution:** Visual breakdown of the Brain's logic (how often it intervenes).

---

## 🩺 System Health & Maintenance

| Feature | Description |
| :--- | :--- |
| **Integrity Scan** | Reconciles `SUM(Transactions)` against `User.Balance` for every account. |
| **Cache Purge** | Flushes Redis hot-state and re-synchronizes with SQL persistence. |
| **Emergency Stop** | Global kill-switch that instantly halts all game engines. |
| **WAL Monitoring** | Real-time tracking of the Write-Ahead Log to ensure no financial data loss. |

---

## 📱 Mobile Administration
The Backoffice is optimized for "On-the-go" management:
*   **Adaptive Grid:** Stats cards stack vertically on mobile.
*   **Scrollable Data:** Tables support horizontal swiping for wide audit logs.
*   **Compact Mode:** Exit and maintenance buttons transform into icons to maximize screen space.
