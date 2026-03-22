namespace AleaSim.Domain.Helpers;

/// <summary>
/// Centralized decimal rounding utilities to prevent precision drift in financial calculations.
/// ALWAYS use these methods for consistency across the platform.
/// </summary>
public static class DecimalExtensions {
    /// <summary>
    /// Rounds a decimal to 2 decimal places using Banker's Rounding (MidpointRounding.ToEven).
    /// Use this for DISPLAY purposes (UI, API responses, reports).
    /// Example: 10.125m → 10.12m, 10.135m → 10.14m
    /// </summary>
    /// <param name="value">The value to round</param>
    /// <returns>Rounded value with 2 decimal places</returns>
    public static decimal RoundForDisplay(this decimal value) {
        return Math.Round(value, 2, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Rounds a decimal to 4 decimal places for STORAGE in database.
    /// Use this when saving balances, bets, wins to ensure consistency.
    /// Keeps more precision than display but prevents infinite precision.
    /// Example: 10.12345m → 10.1235m
    /// </summary>
    /// <param name="value">The value to round</param>
    /// <returns>Rounded value with 4 decimal places</returns>
    public static decimal RoundForStorage(this decimal value) {
        return Math.Round(value, 4, MidpointRounding.ToEven);
    }

    /// <summary>
    /// Formats a decimal as money string with 2 decimal places and currency symbol.
    /// Use this for UI display of currency values.
    /// Example: 1234.5m → "$1,234.50"
    /// </summary>
    /// <param name="value">The amount to format</param>
    /// <param name="currencySymbol">Currency symbol (default: $)</param>
    /// <returns>Formatted money string</returns>
    public static string ToMoneyFormat(this decimal value, string currencySymbol = "$") {
        return $"{currencySymbol}{value.RoundForDisplay():N2}";
    }

    /// <summary>
    /// Validates that a decimal value is safe for financial operations.
    /// Prevents MaxValue, MinValue, NaN-like edge cases.
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <returns>True if safe for calculations</returns>
    public static bool IsSafeForCalculation(this decimal value) {
        return value != decimal.MaxValue 
            && value != decimal.MinValue 
            && value >= 0m 
            && value < 1_000_000_000m; // Cap at 1 billion for sanity
    }

    /// <summary>
    /// Calculates percentage with proper rounding.
    /// Example: 100m.PercentageOf(300m) → 33.33m (not 33.333333...)
    /// </summary>
    /// <param name="value">The part value</param>
    /// <param name="total">The total value</param>
    /// <returns>Percentage rounded to 2 decimals</returns>
    public static decimal PercentageOf(this decimal value, decimal total) {
        if (total == 0m) return 0m;
        return ((value / total) * 100m).RoundForDisplay();
    }

    /// <summary>
    /// Applies a percentage multiplier with proper rounding.
    /// Example: 100m.MultiplyByPercent(10.5m) → 10.50m (100 * 0.105)
    /// </summary>
    /// <param name="value">The base value</param>
    /// <param name="percentage">The percentage (10.5 for 10.5%)</param>
    /// <returns>Result rounded for storage (4 decimals)</returns>
    public static decimal MultiplyByPercent(this decimal value, decimal percentage) {
        return (value * (percentage / 100m)).RoundForStorage();
    }
}
