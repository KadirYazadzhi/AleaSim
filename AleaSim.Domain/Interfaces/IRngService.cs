namespace AleaSim.Domain.Interfaces;

public interface IRngService {
    int GetNextInt(int seed, int sequence, int minValue, int maxValue);
    double GetNextDouble(int seed, int sequence);
}
