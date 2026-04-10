namespace AleaSim.Domain.Models;

public class FruitBlastConfig {
    public int Rows { get; set; } = 5;
    public int Cols { get; set; } = 6;
    public int MinCluster { get; set; } = 5;
    
    // Thresholds for GetWeightedSymbol (base 1000)
    public int FruitThreshold { get; set; } = 970;
    public int AppleThreshold { get; set; } = 975;
    public int StarThreshold { get; set; } = 982;
    public int TntThreshold { get; set; } = 992;
    public int NuclearThreshold { get; set; } = 998;
}
