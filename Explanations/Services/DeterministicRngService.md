# DeterministicRngService Implementation Explanation

`DeterministicRngService.cs` implements the RNG contract with a focus on auditability.

## 🧮 Algorithm
It does not use the system clock. It uses a mathematical combination of:
1.  **Session Seed**: Generated once when the user opens the game.
2.  **Sequence**: Incremented for every single card/spin.

### `unchecked((seed * 397) ^ sequence)`
- **Goal**: To mix the bits of the seed and sequence sufficiently so that `Sequence 1` and `Sequence 2` produce vastly different numbers.
- **Unchecked**: Allows integer overflow. This is desirable in hashing/RNG as it wraps around values instead of crashing.
- **Result**: `new Random(mixedSeed)` produces a predictable stream of numbers for that specific inputs.