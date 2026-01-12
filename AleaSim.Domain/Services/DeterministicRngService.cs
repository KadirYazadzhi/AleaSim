using System.Security.Cryptography;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class DeterministicRngService : IRngService {
    // In a real "Provably Fair" system, we would hash ServerSeed + ClientSeed + Nonce.
    // For now, we upgrade this to use CSPRNG to prevent basic prediction attacks.
    
    public int GetNextInt(int seed, int nonce, int min, int max) {
        // We ignore the weak 'seed' int for security, unless we implement full HMAC hashing later.
        // Using CSPRNG guarantees uniform distribution and unpredictability.
        if (min >= max) return min;
        return RandomNumberGenerator.GetInt32(min, max);
    }

    public double GetNextDouble(int seed, int nonce) {
        // Generates a double between 0.0 and 1.0 using crypto-secure bytes
        byte[] bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        // Convert to UInt64 and divide by MaxValue to get 0..1
        ulong ul = BitConverter.ToUInt64(bytes, 0);
        return (double)ul / (double)ulong.MaxValue;
    }
}