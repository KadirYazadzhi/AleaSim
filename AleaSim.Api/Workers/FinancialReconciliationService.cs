using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Enums;
using AleaSim.Domain.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Api.Workers;

/// <summary>
/// Background service that performs daily financial reconciliation to detect balance discrepancies.
/// Runs at 3 AM UTC daily to verify:
/// - User balances match their transaction history
/// - Pool balances are correct
/// - No phantom balance drift from rounding errors
/// </summary>
public class FinancialReconciliationService : BackgroundService {
    private readonly IServiceProvider _services;
    private readonly ILogger<FinancialReconciliationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Run once per day
    private const int TARGET_HOUR_UTC = 3; // 3 AM UTC

    public FinancialReconciliationService(IServiceProvider services, ILogger<FinancialReconciliationService> logger) {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Financial Reconciliation Service started. Will run daily at {Hour}:00 UTC", TARGET_HOUR_UTC);

        while (!stoppingToken.IsCancellationRequested) {
            try {
                var now = DateTime.UtcNow;
                var nextRun = GetNextRunTime(now);
                var delay = nextRun - now;

                _logger.LogInformation("Next reconciliation scheduled for {NextRun} UTC ({Delay} from now)", 
                    nextRun.ToString("yyyy-MM-dd HH:mm:ss"), delay.ToString(@"hh\:mm\:ss"));

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested) {
                    await RunReconciliation(stoppingToken);
                }
            }
            catch (TaskCanceledException) {
                // Service is stopping, exit gracefully
                break;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Unexpected error in reconciliation scheduler");
                // Wait 1 hour before retrying on error
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("Financial Reconciliation Service stopped");
    }

    private DateTime GetNextRunTime(DateTime current) {
        // Calculate next 3 AM UTC
        var today3AM = current.Date.AddHours(TARGET_HOUR_UTC);
        
        if (current >= today3AM) {
            // Already past 3 AM today, schedule for tomorrow
            return today3AM.AddDays(1);
        }
        
        return today3AM;
    }

    private async Task RunReconciliation(CancellationToken cancellationToken) {
        using var scope = _services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        _logger.LogInformation("=== STARTING FINANCIAL RECONCILIATION ===");
        var startTime = DateTime.UtcNow;
        
        var discrepancies = new List<string>();

        try {
            // 1. Verify User Balances
            _logger.LogInformation("Step 1: Verifying user balances...");
            var userDiscrepancies = await VerifyUserBalances(repo);
            discrepancies.AddRange(userDiscrepancies);

            // 2. Verify Pool Balances
            _logger.LogInformation("Step 2: Verifying game pool balances...");
            var poolDiscrepancies = await VerifyPoolBalances(repo);
            discrepancies.AddRange(poolDiscrepancies);

            // 3. Verify Total System Balance
            _logger.LogInformation("Step 3: Verifying total system balance...");
            var systemDiscrepancies = await VerifySystemBalance(repo);
            discrepancies.AddRange(systemDiscrepancies);

            var duration = DateTime.UtcNow - startTime;
            
            if (discrepancies.Count == 0) {
                _logger.LogInformation("✅ RECONCILIATION PASSED - No discrepancies found (Duration: {Duration}ms)", duration.TotalMilliseconds);
                
                auditService.LogEvent(
                    "RECONCILIATION_SUCCESS", 
                    $"Daily reconciliation completed successfully in {duration.TotalMilliseconds:F0}ms", 
                    "SYSTEM", 
                    "All balances verified"
                );
            }
            else {
                _logger.LogWarning("⚠️ RECONCILIATION FAILED - Found {Count} discrepancies (Duration: {Duration}ms)", 
                    discrepancies.Count, duration.TotalMilliseconds);
                
                foreach (var discrepancy in discrepancies) {
                    _logger.LogWarning("DISCREPANCY: {Message}", discrepancy);
                }

                auditService.LogEvent(
                    "RECONCILIATION_FAILURE", 
                    $"Found {discrepancies.Count} balance discrepancies", 
                    "SYSTEM", 
                    string.Join(" | ", discrepancies)
                );

                // TODO: Send alert to admins (Email, Slack, etc.)
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Reconciliation failed with exception");
            auditService.LogEvent("RECONCILIATION_ERROR", ex.Message, "SYSTEM", ex.StackTrace ?? "");
        }

        _logger.LogInformation("=== RECONCILIATION COMPLETE ===");
    }

    private async Task<List<string>> VerifyUserBalances(IGameRepository repo) {
        var discrepancies = new List<string>();
        
        // Get all users with non-zero balance
        var allUsers = repo.GetAllUsers(); // Assumes this method exists or implement GetAllActiveUsers

        foreach (var user in allUsers.Where(u => u.Balance != 0 || u.BonusBalance != 0)) {
            try {
                // Calculate expected balance from transaction history
                var transactions = repo.GetUserTransactions(user.Id, 10000); // Get all recent transactions
                
                decimal calculatedBalance = transactions
                    .OrderBy(t => t.Timestamp)
                    .Aggregate(0m, (balance, txn) => (balance + txn.Amount).RoundForStorage());

                var actualBalance = user.Balance.RoundForStorage();
                var difference = Math.Abs(actualBalance - calculatedBalance);

                // Allow small rounding differences (0.01) but flag larger ones
                if (difference > 0.01m) {
                    var msg = $"User {user.Username} ({user.Id}): Expected {calculatedBalance:F4}, Actual {actualBalance:F4}, Diff {difference:F4}";
                    discrepancies.Add(msg);
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error verifying balance for user {UserId}", user.Id);
                discrepancies.Add($"User {user.Id}: Error - {ex.Message}");
            }
        }

        return discrepancies;
    }

    private async Task<List<string>> VerifyPoolBalances(IGameRepository repo) {
        var discrepancies = new List<string>();
        
        // Get common games by type
        var gameTypes = new[] { "slot", "roulette", "dice", "blackjack", "baccarat", "crash", "plinko", "fruitblast" };

        foreach (var gameType in gameTypes) {
            try {
                var game = repo.GetGameByType(gameType);
                if (game == null) continue;

                // Pool balance should be sum of all bets minus all wins for this game
                // This is simplified - you might need game-specific logic
                var poolBalance = game.PoolBalance.RoundForStorage();

                // Verify pool is not negative (critical error)
                if (poolBalance < 0) {
                    discrepancies.Add($"Game {game.Name} ({game.Id}): NEGATIVE pool balance {poolBalance:F4}");
                }

                // Verify pool is reasonable (not absurdly high)
                if (poolBalance > 100_000_000m) {
                    discrepancies.Add($"Game {game.Name} ({game.Id}): Suspiciously high pool {poolBalance:F4}");
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error verifying pool for game {GameType}", gameType);
                discrepancies.Add($"Game {gameType}: Error - {ex.Message}");
            }
        }

        return discrepancies;
    }

    private async Task<List<string>> VerifySystemBalance(IGameRepository repo) {
        var discrepancies = new List<string>();

        try {
            // Total user balances
            var allUsers = repo.GetAllUsers();
            var totalUserBalance = allUsers.Sum(u => u.Balance + u.BonusBalance).RoundForStorage();

            // Total pool balances
            decimal totalPoolBalance = 0m;
            var gameTypes = new[] { "slot", "roulette", "dice", "blackjack", "baccarat", "crash", "plinko", "fruitblast" };
            foreach (var gameType in gameTypes) {
                var game = repo.GetGameByType(gameType);
                if (game != null) {
                    totalPoolBalance += game.PoolBalance;
                }
            }
            totalPoolBalance = totalPoolBalance.RoundForStorage();

            // Total should be positive and reasonable
            var totalSystemBalance = totalUserBalance + totalPoolBalance;

            _logger.LogInformation("System Balance Summary: Users={Users:F2}, Pools={Pools:F2}, Total={Total:F2}", 
                totalUserBalance, totalPoolBalance, totalSystemBalance);

            // Sanity checks
            if (totalUserBalance < 0) {
                discrepancies.Add($"Total user balance is NEGATIVE: {totalUserBalance:F4}");
            }

            if (totalSystemBalance > 1_000_000_000m) {
                discrepancies.Add($"Total system balance exceeds 1 billion: {totalSystemBalance:F4}");
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error verifying system balance");
            discrepancies.Add($"System Balance: Error - {ex.Message}");
        }

        return discrepancies;
    }
}
