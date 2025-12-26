# UserConfiguration - Identity Schema

Configures the `User` entity.

## 🛠️ Schema Rules

### `HasIndex(u => u.Username).IsUnique()`
- **Constraint**: Guarantees no two users can register with the same name.
- **Performance**: Makes login lookups (`SELECT * FROM Users WHERE Username = 'Bob'`) extremely fast (`O(log n)` instead of `O(n)`).

### Balance Precision
- **`Balance`**: `(18, 2)`.
- **Criticality**: This is the single most important column in the database. Incorrect precision here could lead to users losing money or the casino leaking fractional cents that add up to massive losses (classic "Office Space" / "Superman III" logic).
