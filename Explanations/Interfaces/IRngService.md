# IRngService Interface Explanation

The `IRngService` interface abstracts the generation of random numbers. This abstraction is critical for testability and auditing.

## 🛠️ Method Contracts

### `GetNextInt(int seed, int sequence, ...)`
- **Deterministic**: Given the same `seed` and `sequence`, this MUST return the same number.
- **Usage**: Used for selecting cards, slot stops, or roulette numbers.

### `GetNextDouble(int seed, int sequence)`
- **Usage**: Used for probability checks (e.g., Jackpot trigger chance < 0.00001).