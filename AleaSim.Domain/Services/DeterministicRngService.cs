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
        // Use a stable bitwise combination instead of HashCode.Combine for cross-platform determinism
        // unchecked allows overflow which is fine/desired for mixing
        int combinedSeed = unchecked((seed * 397) ^ sequence); 
        return new Random(combinedSeed);
    }
}
