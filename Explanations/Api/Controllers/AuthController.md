# AuthController Explanation

`AuthController.cs` handles user identity, registration, and session token generation.

## 🛠️ Endpoints

### `POST api/auth/register`
- **Logic**:
    1. Checks if username exists (Duplicate check).
    2. Hashes the password using `IPasswordHasher` (PBKDF2).
    3. Creates a new `User` with a default balance of $1000.
    4. Saves to database.

### `POST api/auth/login`
- **Logic**:
    1. Fetches user by username.
    2. Verifies the password using `_passwordHasher.VerifyPassword` (Re-hashes input with stored salt).
    3. **Token Generation**: Creates a JWT (JSON Web Token) containing:
        - `Name`: Username
        - `NameIdentifier`: UserId (Guid)
        - `Role`: User/Admin
    4. **Result**: Returns the token which must be sent in the `Authorization: Bearer` header for all other requests.
