# AuditEvent - Immutable Audit Log Entity

The `AuditEvent` class represents a single, immutable record of an action taken within the system. It is the fundamental building block of the **Audit Trail**.

## 🎯 Purpose
In regulated gambling environments (and financial systems in general), it is critical to prove **who** did **what**, **when**, and **that the records have not been altered**. This entity provides that proof.

## 🏗️ Property Breakdown

| Property | Type | Description |
| :--- | :--- | :--- |
| **`Id`** | `Guid` | A Globally Unique Identifier. Ensures every event has a unique addressable ID across distributed systems, unlike sequential integers. |
| **`Timestamp`** | `DateTime` | The exact time the event occurred. Crucial for reconstructing the timeline of an incident. Should always be stored in **UTC** to avoid timezone conflicts. |
| **`EventType`** | `string` | Categorizes the action (e.g., `"GameStart"`, `"JackpotWin"`, `"Login"`). Used for filtering logs. |
| **`Description`** | `string` | A human-readable summary of what happened. |
| **`UserId`** | `string` | Links the event to a specific actor. Stored as a string to allow flexibility (could be a GUID or an external system ID). |
| **`MetadataJson`** | `string` | A "Catch-All" field. Stores complex data related to the event (e.g., the specific cards dealt, the old and new balance) in a JSON format. This avoids creating hundreds of columns for different event types. |

## 🔐 Cryptographic Integrity (The Blockchain Concept)

This entity implements a **Hash Chain** to prevent tampering:

### `PreviousHash`
- Stores the `Hash` of the *immediately preceding* audit event.
- **Purpose**: Creates a link. If Event A connects to Event B, and Event B connects to Event C, you cannot delete or change Event B without breaking the link in Event C.

### `Hash`
- A digital signature generated from this event's data **PLUS** the `PreviousHash`.
- **Formula**: `Hash = SHA256(Timestamp + EventType + UserId + Metadata + PreviousHash)`
- **Verification**: The system can re-calculate this hash at any time. If the re-calculated hash doesn't match the stored `Hash`, the data has been tampered with.