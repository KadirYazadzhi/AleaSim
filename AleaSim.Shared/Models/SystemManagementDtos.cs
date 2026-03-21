using System;
using System.Collections.Generic;

namespace AleaSim.Shared.Models;

public class TournamentDto {
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal PrizePool { get; set; }
    public bool IsActive { get; set; }
    public List<string> IncludedGames { get; set; } = new();
}

public class SystemErrorDto {
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
