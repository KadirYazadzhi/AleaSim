# UserConfiguration Schema Explanation

Configures the `User` table properties.

## 🛠️ Schema Rules

- **`Username`**: Required, Max 50 chars, Unique Index.
- **`Email`**: Max 100 chars.
- **`PasswordHash`**: Max 255 chars. Large enough to hold the Base64 Salt + Hash string.
- **`Balance`**: `(18, 2)` Precision.