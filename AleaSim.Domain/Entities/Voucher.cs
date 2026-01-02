namespace AleaSim.Domain.Entities;

public class Voucher {
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty; // e.g. "WELCOME100"
    public decimal Amount { get; set; }
    public decimal WageringRequirement { get; set; }
    
    public int MaxUses { get; set; }
    public int CurrentUses { get; set; }
    
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserVoucher {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid VoucherId { get; set; }
    public DateTime RedeemedAt { get; set; }
}
