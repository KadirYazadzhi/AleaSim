# AuditService - Implementation of Secure Logging

`AuditService.cs` is the in-memory implementation of the secure audit trail.

## ⚙️ Key Implementation Details

### Thread Safety (`lock`)
```csharp
lock (_logs) { ... }
```
- **Why?** In a web server, multiple users perform actions simultaneously. Without locking, two logs could try to write to the list at the exact same nanosecond, causing data corruption or Race Conditions. The `lock` ensures logs are written strictly one after another.

### The Blockchain Logic (`CalculateHash`)
The service implements a simplified blockchain:
1.  **Genesis**: Starts with a hardcoded `_lastHash = "GENESIS"`.
2.  **Chaining**: When a new log arrives:
    - It reads `_lastHash` from the *previous* log.
    - It calculates its own `Hash`.
    - It updates `_lastHash` to its own new hash.

#### Hash Formula
```csharp
string data = $"{ev.Timestamp:O}|{ev.EventType}|{ev.UserId}|{ev.MetadataJson}|{ev.PreviousHash}";
byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(data));
```
- **Implication**: Every single character in the log affects the hash. Changing the timestamp by 1 millisecond changes the hash completely.

### Integrity Check (`VerifyIntegrity`)
This method proves the database hasn't been hacked.
1.  It recalculates the hash for *every* log in history.
2.  If `CalculatedHash != StoredHash`, data was edited.
3.  If `Log[B].PreviousHash != Log[A].Hash`, a record was deleted.
