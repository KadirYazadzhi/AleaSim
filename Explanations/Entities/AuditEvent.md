# AuditEvent Entity Explanation

The `AuditEvent` class is the fundamental unit of the system's security log. It is designed to be immutable and tamper-evident.

## 📦 Properties

### Core Data
- **`Id`** (`Guid`): Unique event ID.
- **`Timestamp`** (`DateTime`): UTC time of occurrence.
- **`EventType`** (`string`): Categorical code (e.g., "LOGIN", "BET_PLACED", "JACKPOT_TRIGGER").
- **`UserId`** (`string`): The actor associated with the event. Stored as a string to support system events (where UserId might be "SYSTEM") or external IDs.
- **`Description`** (`string`): Human-readable summary.
- **`MetadataJson`** (`string`): Flexible context storage.
    - *Example*: `{ "ip_address": "127.0.0.1", "browser": "Chrome" }`

### Integrity Mechanism (Blockchain-lite)
- **`Hash`** (`string`): SHA-256 signature of this event's data.
- **`PreviousHash`** (`string`): The `Hash` of the immediately preceding event.
    - **Concept**: This links events together. Modifying an old event would invalidate its hash, and subsequently invalidate the `PreviousHash` of the next event, breaking the chain and alerting auditors.
