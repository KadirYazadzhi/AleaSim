namespace AleaSim.Client.Models;

public class SlotState {
    public bool IsBonusActive { get; set; }
    public int BonusLives { get; set; }
    public bool IsRespinActive { get; set; }
    public bool IsGambleActive { get; set; }
    public decimal PendingGambleWin { get; set; }
    public decimal Denomination { get; set; } = 0.01m;
}

public class SlotStateResponse {
    public bool IsBonusActive { get; set; }
    public bool IsRespinActive { get; set; }
    public bool WasNudged { get; set; }
}
