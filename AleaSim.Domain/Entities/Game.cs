using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace AleaSim.Domain.Entities;

public class Game 
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // Title alias
    public string Type { get; set; } = string.Empty;
    public string Provider { get; set; } = "AleaSim Originals";
    public decimal MinBet { get; set; } = 0.1m;
    public decimal MaxBet { get; set; } = 1000m;
    public decimal TargetRTP { get; set; } = 0.95m; // double Rtp alias
    public string? ConfigurationJson { get; set; } 
    public bool IsActive { get; set; }
    public decimal PoolBalance { get; set; } = 0m; 

    [NotMapped]
    public string Title { get => Name; set => Name = value; }
    
    [NotMapped]
    public double Rtp { get => (double)TargetRTP; set => TargetRTP = (decimal)value; }
}
