namespace AleaSim.Domain.Entities;

    public class Game {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public decimal MinBet { get; set; }
        public decimal MaxBet { get; set; }
    public decimal TargetRTP { get; set; } // e.g. 0.95 (95%)
    public string? ConfigurationJson { get; set; } // JSON storage for game-specific rules (strips, paylines)
        public bool IsActive { get; set; }
        
        // The "Bank" or "Cycle" balance. 
        // Money In adds (Amount * RTP). Money Out subtracts Win.
        public decimal PoolBalance { get; set; } = 0m; 
    }
