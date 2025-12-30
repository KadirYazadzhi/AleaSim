namespace AleaSim.Domain.Enums;

public enum SpinProfile {
    Standard,       // Normal random play
    HighVolatility, // Risky play (fewer win checks, bigger potential)
    LowVolatility,  // Retention play (frequent small wins)
    ForceTeaser,    // Force a "Near Miss" (visual excitement, no win)
    ForceWin        // (Admin/Bonus) Force a win if possible
}
