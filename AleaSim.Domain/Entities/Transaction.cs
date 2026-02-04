namespace AleaSim.Domain.Entities;

public enum TransactionType {
    Deposit,
    Withdrawal,
    BonusAwarded,
    BonusConverted,
    WageringRequirementMet,
    Bet,
    Win,
    Bonus,
    Faucet
}

public class Transaction {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    
    // Balance after transaction
    public decimal ResultingBalance { get; set; }
}
