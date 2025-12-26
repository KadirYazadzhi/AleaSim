# IRngService - Randomness Contract

The `IRngService` is the most critical interface for a gambling system. It abstracts *how* numbers are chosen.

## 🎯 Purpose
To allow swapping between different RNG strategies without breaking the games.
1.  **Production**: Might use a Hardware RNG (TRNG) or a cryptographically secure PRNG.
2.  **Testing/Auditing**: Uses a `DeterministicRngService` where `Seed + Input = Same Output`.

## 🛠️ Method Contracts

### `int GetNextInt(int seed, int sequence, int minValue, int maxValue)`
- **Goal**: Integer selection (e.g., Card Index 0-51, Roulette Number 0-36).
- **Parameters**:
    - `seed`: The Session's master key.
    - `sequence`: The specific step (e.g., Round 1, Round 2, Card 3). **Crucial**: Changing the sequence ensures we don't get the same card every time we draw.

### `double GetNextDouble(int seed, int sequence)`
- **Goal**: Probability checks.
- **Return**: A value between `0.0` and `1.0`.
- **Usage**: Used for Jackpot checks (e.g., `if (result < 0.0001) Win()`).
