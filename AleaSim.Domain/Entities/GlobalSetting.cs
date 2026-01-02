namespace AleaSim.Domain.Entities;

public class GlobalSetting {
    public string Key { get; set; } = string.Empty; // e.g., "GlobalTargetRtp", "EmergencyStop"
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
}
