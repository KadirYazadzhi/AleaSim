# Program.cs Explanation

The entry point and configuration root of the ASP.NET Core Web API.

## ⚙️ Key Configurations

### Dependency Injection (DI)
- **Database**: Registers `AleaSimDbContext` with **MySQL** (`UseMySql`).
- **Repositories**: Registers `EfGameRepository` as `Scoped` (one per request).
- **Services**:
    - `IRngService` -> `DeterministicRngService` (Singleton)
    - `IPasswordHasher` -> `PasswordHasher` (Singleton)
- **Game Engines**: Registered as **Singletons**.
    - *Why Singleton?* They hold state (Active Sessions) in memory (though moving towards DB persistence, the current architecture still treats engines as long-lived services).
- **Game Factory**: A lambda factory `Func<string, IGame>` is registered to switch between engines based on a string key.

### Authentication
- Configures **JWT Bearer Authentication**.
- Reads `Jwt:Key` from settings to sign/verify tokens.

### Database Seeding
- On startup, it creates a scope (`app.Services.CreateScope()`).
- **`EnsureCreated()`**: Creates the DB schema if it doesn't exist.
- **Seed Data**:
    - Inserts default Games (Slot, Roulette, Blackjack).
    - Inserts a default `admin` user (password: "admin").

### Middleware
- **Swagger**: Enabled in Development for API testing.
- **SignalR**: Maps `/gamehub`.
