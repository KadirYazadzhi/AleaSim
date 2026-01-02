namespace AleaSim.Client.Models;

public class SlotResultGrid {
    public int[][] Grid { get; set; } = Array.Empty<int[]>();
    public List<BonusSymbolUI>? BonusSymbols { get; set; }
    public bool Nudge { get; set; }
    public decimal CoinWin { get; set; }
}

public class BonusSymbolUI {
    public int Row { get; set; }
    public int Col { get; set; }
    public int Type { get; set; }
    public decimal Value { get; set; }
}

public class SlotGameStateUI {
    public bool IsRespinActive { get; set; }
    public int RespinLives { get; set; }
    public bool IsBonusActive { get; set; }
    public int BonusLives { get; set; }
    public bool CanGamble { get; set; }
    public decimal PendingGambleAmount { get; set; }
}
