namespace AleaSim.Domain.Helpers;

public static class ValidationHelper {
    public static bool IsValidDecimal(decimal value) {
        // Check for special values that would cause issues
        return value != decimal.MaxValue 
            && value != decimal.MinValue 
            && value >= 0;
    }

    public static decimal ValidateAndClamp(decimal value, decimal min, decimal max) {
        if (!IsValidDecimal(value)) {
            throw new ArgumentException($"Invalid decimal value: {value}");
        }
        
        return Math.Clamp(value, min, max);
    }
}
