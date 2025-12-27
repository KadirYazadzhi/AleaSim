# User Entity Explanation

The `User` class represents an authenticated actor within the AleaSim platform. It is the central entity for identity, authorization, and financial tracking.

## 📦 Properties

### Identity & Authentication
- **`Id`** (`Guid`): Unique system-wide identifier.
- **`Username`** (`string`): Unique display name used for login and public display.
- **`Email`** (`string`): Contact email address, used for account recovery or notifications.
- **`PasswordHash`** (`string`): **Security Critical**. Does not store the actual password. Stores a hashed version (likely using BCrypt or Argon2 via `PasswordHasher`) to protect user credentials in case of a data breach.

### Financial State
- **`Balance`** (`decimal`): The user's current available funds.
    - **Precision**: Must be handled with high precision (18, 2) to avoid floating-point errors.
    - **Concurrency**: Modifications to this field must be transactional to prevent double-spending or race conditions.

### Access Control
- **`Role`** (`Role` enum): Defines the user's permission level (User, Admin, Auditor).
- **`IsActive`** (`bool`): Account status flag. Allows admins to ban or suspend users without deleting their data (Soft Delete / Suspension).

### Metadata
- **`CreatedAt`** (`DateTime`): Timestamp of registration.