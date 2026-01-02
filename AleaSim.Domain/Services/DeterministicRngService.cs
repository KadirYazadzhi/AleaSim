using AleaSim.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace AleaSim.Domain.Services;

public class DeterministicRngService : IRngService {
    
    public double GetNextDouble(int seed, int sequence) {
        // Legacy support or fallback
        return GetProvablyFairDouble(seed.ToString(), "default", sequence);
    }

    public int GetNextInt(int seed, int sequence, int min, int max) {
        double val = GetNextDouble(seed, sequence);
        return min + (int)(val * (max - min));
    }

    // Standard Provably Fair Algorithm
    public double GetProvablyFairDouble(string serverSeed, string clientSeed, int nonce) {
        string combo = $"{clientSeed}:{nonce}";
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(serverSeed));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(combo));

        // Use the first 8 bytes to create a double between 0 and 1
        ulong value = BitConverter.ToUInt64(hash, 0);
        return (double)value / ulong.MaxValue;
    }

    public string GenerateNewServerSeed() {
        byte[] randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToHexString(randomBytes).ToLower();
    }

    public string HashSeed(string seed) {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(bytes).ToLower();
    }
}