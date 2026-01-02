namespace AleaSim.Domain.Entities;

public class UserProgression {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    
    public int CurrentLevel { get; set; } = 1;
    public decimal CurrentXP { get; set; } = 0;
    public int SkillPoints { get; set; } = 0;
    
    // Total accumulated XP over lifetime
    public decimal LifetimeXP { get; set; } = 0;
}
