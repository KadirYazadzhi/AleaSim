# AuthDtos Explanation

Data Transfer Objects (DTOs) for authentication flows.

## 📦 Models
- **`LoginRequest`**: `Username`, `Password`.
- **`LoginResponse`**: Returns the JWT `Token`, along with `Username` and `Role` for the frontend UI to adjust itself.
- **`RegisterRequest`**: Adds `Email` to the standard credentials.
