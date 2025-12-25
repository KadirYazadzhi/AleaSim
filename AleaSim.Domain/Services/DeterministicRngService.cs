using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class DeterministicRngService : IRngService {
    public int GetNextInt(int seed, int sequence, int minValue, int maxValue) {
        var random = CreateRandom(seed, sequence);
        return random.Next(minValue, maxValue);
    }

    public double GetNextDouble(int seed, int sequence) {
        var random = CreateRandom(seed, sequence);
        return random.NextDouble();
    }

    private Random CreateRandom(int seed, int sequence) {
        // Using HashCode.Combine to mix seed and sequence for a deterministic starting point
        int combinedSeed = HashCode.Combine(seed, sequence);
        return new Random(combinedSeed);
    }
}
