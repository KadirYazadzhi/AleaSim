# AuditEventConfiguration - Audit Schema Definition

This file defines how the `AuditEvent` entity is mapped to the SQL database.

## 🛠️ Schema Rules

### Indexing (`HasIndex`)
- **`Timestamp`**: Critical for performance. Audit logs are almost always queried by time range (e.g., "Show me logs from yesterday").
- **`UserId`**: Optimizes looking up a specific user's history.
- **`Hash`**: Marked as `.IsUnique()`. This is a database-level constraint that guarantees no two events can ever have the same hash signature. This prevents hash collisions and aids in integrity verification.

### Property Constraints
- **`EventType`**: `HasMaxLength(50)`. Saves space. We don't need `text` (unlimited) for short codes like "LOGIN".
- **`Hash`**: `IsRequired()`. Ensures the cryptographic chain is never broken by null values.

### 📝 Note on Immutability
The comment mentions that strict "Append-Only" enforcement is hard in EF Core without triggers.
- **Implication**: While this configuration defines the *shape*, the application code (Repository) is responsible for ensuring logs are never called with `Update()` or `Delete()`.
