namespace AleaSim.Domain.Constants;

public static class GameConstants {
    // Cashback Configuration
    public const decimal BASE_CASHBACK_RATE = 0.10m; // 10% base cashback
    public const decimal CASHBACK_PER_LEVEL = 0.01m; // 1% per level
    public const int MAX_CASHBACK_LEVEL = 40; // Max 40 levels = 50% total
    
    // Shadow Balance
    public const decimal SHADOW_BALANCE_RATE = 0.95m; // 95% of bet goes to shadow
    
    // Flow State Thresholds (seconds)
    public const double FLOW_STATE_INTERVAL = 2.5;
    public const double BORED_STATE_INTERVAL = 7.0;
    
    // Volatility Modifiers
    public const double FLOW_VOLATILITY = 2.0;
    public const double BORED_VOLATILITY = 0.5;
    public const double NORMAL_VOLATILITY = 1.0;
    
    // Betting Limits
    public const decimal MIN_BET = 0.01m;
    public const decimal MAX_BET = 1000000m;
    public const decimal MAX_WIN = 10000000m;
    
    // Wagering Minimum
    public const decimal MIN_WAGERING_FOR_RTP = 100m;
}
