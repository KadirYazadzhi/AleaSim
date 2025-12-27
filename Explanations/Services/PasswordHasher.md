# PasswordHasher Implementation Explanation

`PasswordHasher.cs` is a standard security service for user credentials.

## 🔐 Cryptography

### Algorithm: PBKDF2 (RFC 2898)
- **Functions**: Uses `Rfc2898DeriveBytes.Pbkdf2`.
- **Algorithm**: `SHA256`.
- **Iterations**: `100,000`. This makes the hashing process "slow" on purpose, preventing hackers from brute-forcing millions of passwords per second.

### Salt
- **Size**: 128-bit (16 bytes).
- **Purpose**: A random value added to the password before hashing. Prevents "Rainbow Table" attacks (pre-computed hash lists).

### Storage Format
- Returns: `Base64(Salt).Base64(Hash)`.
- This allows the `VerifyPassword` method to extract the exact salt used for creation and re-run the process to check validity.
