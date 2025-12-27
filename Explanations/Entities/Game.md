# Game Entity Explanation

The `Game` class defines the configuration, rules, and limits for a specific game type available in the catalog.

## 📦 Properties

### Descriptive
- **`Id`** (`Guid`): Unique identifier for the game definition.
- **`Name`** (`string`): Display name (e.g., "Mega Fortune Slots").
- **`Type`** (`string`): **Discriminator**. Categorizes the game (e.g., "Slot", "Roulette", "Blackjack"). This string is often used by the frontend to decide which assets to load and by the backend factory to instantiate the correct `IGame` engine.
- **`Description`** (`string`): Marketing or instructional text.

### Risk & Math Configuration
- **`MinBet`** & **`MaxBet`** (`decimal`): Operational boundaries.
    - `MinBet`: Prevents spamming insignificant transactions.
    - `MaxBet`: Limits the casino's exposure to volatility per round.
- **`TargetRTP`** (`double`): The theoretical Return to Player percentage (e.g., 0.96 for 96%). The `RtpEngine` uses this value to monitor actual performance against expected math.

### State
- **`IsActive`** (`bool`): Master switch to enable/disable the game in the lobby.
