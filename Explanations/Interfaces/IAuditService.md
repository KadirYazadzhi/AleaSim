# IAuditService Interface Explanation

The `IAuditService` defines the contract for secure logging.

## 🛠️ Method Contracts

### `LogEvent`
- **Purpose**: Creates a new immutable record of a system action.
- **Params**: `eventType`, `description`, `userId`, `metadataJson`.

### `VerifyIntegrity`
- **Purpose**: A self-check function that recalculates the hash chain of the entire log history to detect if any database rows have been tampered with manually.

### `GetLogs`
- **Purpose**: Retrieval for the Auditor dashboard.