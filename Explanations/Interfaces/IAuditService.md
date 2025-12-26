# IAuditService - Audit Interface

The `IAuditService` defines the contract for a secure, tamper-evident logging system.

## 🎯 Purpose
To abstract the storage and verification mechanism of the audit trail. This allows the underlying implementation (SQL, Blockchain, Flat File) to change without affecting the rest of the application.

## 🛠️ Method Contracts

### `void LogEvent(string eventType, string description, string userId, string metadataJson)`
- **Goal**: Persist a new action.
- **Parameters**:
    - `eventType`: Short code for indexing (e.g., "LOGIN_FAIL").
    - `metadataJson`: Structured data context (e.g., `{ "IP": "192.168.1.1" }`).
- **Expectation**: Implementation must calculate the hash and link it to the previous record *before* saving.

### `IEnumerable<AuditEvent> GetLogs()`
- **Goal**: Retrieve history for review.
- **Usage**: Used by the `Auditor` role dashboard.

### `bool VerifyIntegrity()`
- **Goal**: Self-Health Check.
- **Logic**: The service must iterate through all stored logs.
    1. Check if `Log[N].PreviousHash` == `Log[N-1].Hash`.
    2. Check if `Log[N].Hash` == `SHA256(Log[N].Data)`.
- **Return**: `true` if the chain is valid, `false` if *any* record has been modified externally.
