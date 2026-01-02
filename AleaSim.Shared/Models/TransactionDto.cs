namespace AleaSim.Shared.Models;

public class TransactionDto {
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public decimal ResultingBalance { get; set; }
}
