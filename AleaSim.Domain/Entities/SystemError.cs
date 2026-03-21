namespace AleaSim.Domain.Entities;

public class SystemError {
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
