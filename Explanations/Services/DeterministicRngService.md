# DeterministicRngService - Audit-Ready Randomness

`DeterministicRngService.cs` is a specialized implementation of `IRngService` designed for **Reproducibility**.

## ❓ The Problem
Standard `new Random()` in C# uses the current system time as a seed. This means if you run the code now and 5 seconds later, you get different results.
- **Issue**: If an auditor asks, "Why did this user get a Royal Flush?", you cannot prove it was fair. You can't "replay" the moment.

## ✅ The Solution: Deterministic Seeding
This service does **not** use time. It uses inputs provided by the game engine.

```csharp
int combinedSeed = HashCode.Combine(seed, sequence);
return new Random(combinedSeed);
```

### How it works
1.  **Input 1 (`seed`)**: Generated once when the Game Session starts. Stored in the database.
2.  **Input 2 (`sequence`)**: Increments with every card drawn or spin made (1, 2, 3...).
3.  **Result**: `HashCode.Combine(12345, 1)` will *always* produce the same hash. Therefore, `new Random(hash)` will *always* produce the same "random" number.

## 🛡️ Benefits
1.  **Replayability**: We can re-run the entire game session from the logs and get the exact same cards.
2.  **Fairness Proof**: We can prove the server didn't "change" the card to make the player lose, because the seed was committed *before* the bet was placed.
