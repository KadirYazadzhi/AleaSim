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

public class PlayerSearchResultDto {
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal BonusBalance { get; set; }
    public string Role { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public decimal TotalWagered { get; set; }
    public decimal TotalWon { get; set; }
    public int RiskScore { get; set; }
    public List<string> RedFlags { get; set; } = new();
    public bool IsActive { get; set; }
}

public class UpdateBalanceDto {
    public decimal NewBalance { get; set; }
}

public class ToggleStatusDto {
    public bool IsActive { get; set; }
}

public class UpdateNotesDto {
    public string Notes { get; set; } = string.Empty;
}
