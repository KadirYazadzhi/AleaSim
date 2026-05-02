using System.Security.Cryptography;
using System.Text;
using AleaSim.Domain.Interfaces;

namespace AleaSim.Domain.Services;

public class DeterministicRngService : IRngService {
    // A truly "Provably Fair" system using HMAC-SHA256.
    // The result is derived from ServerSeed + ClientSeed + Nonce.
    
    public int GetNextInt(string serverSeed, string clientSeed, int nonce, int min, int max) {
        if (min >= max) return min;
        
        // SECURITY: Fallback to True Cryptographic Randomness if seeds are empty
        if (string.IsNullOrEmpty(serverSeed) || string.IsNullOrEmpty(clientSeed)) {
            return RandomNumberGenerator.GetInt32(min, max);
        }

        double randomValue = GetNextDouble(serverSeed, clientSeed, nonce);
        return (int)Math.Floor(min + (randomValue * (max - min)));
    }

    public double GetNextDouble(string serverSeed, string clientSeed, int nonce) {
        if (string.IsNullOrEmpty(serverSeed) || string.IsNullOrEmpty(clientSeed)) {
            // Generate a secure random double
            byte[] bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            ulong randomUlong = BitConverter.ToUInt64(bytes, 0);
            return (double)randomUlong / (double)ulong.MaxValue;
        }

        // Combined Seed: serverSeed:clientSeed:nonce
        string combined = $"{serverSeed}:{clientSeed}:{nonce}";
        byte[] key = Encoding.UTF8.GetBytes(serverSeed);
        byte[] message = Encoding.UTF8.GetBytes($"{clientSeed}:{nonce}");

        using var hmac = new HMACSHA256(key);
        byte[] hash = hmac.ComputeHash(message);

        // Take the first 8 bytes of the hash to create a 64-bit unsigned integer
        // This ensures the outcome is deterministic based on the seeds.
        ulong value = BitConverter.ToUInt64(hash, 0);
        
        // Convert to a double between 0.0 and 1.0
        return (double)value / (double)ulong.MaxValue;
    }

    // Legacy support for integer seeds (Internal use)
    public int GetNextInt(int seed, int nonce, int min, int max) {
        return GetNextInt(seed.ToString(), "legacy", nonce, min, max);
    }

    public double GetNextDouble(int seed, int nonce) {
        return GetNextDouble(seed.ToString(), "legacy", nonce);
    }
}