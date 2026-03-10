namespace AleaSim.Domain.Interfaces;

public interface IRngService {
    int GetNextInt(int seed, int sequence, int minValue, int maxValue);
    double GetNextDouble(int seed, int sequence);
    
    // Provably Fair (New)
    int GetNextInt(string serverSeed, string clientSeed, int nonce, int min, int max);
    double GetNextDouble(string serverSeed, string clientSeed, int nonce);
}
