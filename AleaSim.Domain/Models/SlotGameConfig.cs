using System.Collections.Generic;

namespace AleaSim.Domain.Models;

public class SlotGameConfig {
    public int Rows { get; set; } = 4;
    public int Cols { get; set; } = 5;
    public int PaylinesCount { get; set; } = 20;
    
    // Symbols
    public int WildSymbol { get; set; } = 8;
    public int ScatterSymbol { get; set; } = 10; // Bell
    public int CollectSymbol { get; set; } = 11;
    public int GoldenSymbol { get; set; } = 12;

    // Strips per reel (simplified as one strip for now, can be extended to [][] for 5 reels)
    public int[] BaseStrip { get; set; } = Array.Empty<int>();

    // Paylines definition
    public int[][] Paylines { get; set; } = Array.Empty<int[]>();

    // Paytable: Symbol ID -> Multipliers for 3, 4, 5 matches
    public Dictionary<int, decimal[]> Paytable { get; set; } = new();
}
