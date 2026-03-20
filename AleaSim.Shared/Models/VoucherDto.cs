namespace AleaSim.Shared.Models;

public class VoucherDto {
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int MaxUses { get; set; }
    public int CurrentUses { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public bool ShowDetails { get; set; }
    public List<VoucherUsageDto> UsageHistory { get; set; } = new();
}

public class VoucherUsageDto {
    public string Username { get; set; } = string.Empty;
    public DateTime RedeemedAt { get; set; }
}
