namespace AleaSim.Shared.Models;

public class InjectBonusDto {
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ForceCooldownDto {
    public int DurationMinutes { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SetRtpDto {
    public decimal TargetRtp { get; set; }
}

public class EmergencyStopDto {
    public bool Enabled { get; set; }
}

public class SetVolatilityDto {
    public string Mode { get; set; } = string.Empty;
}
