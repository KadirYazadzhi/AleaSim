using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Extensions;
using AleaSim.Persistence;
using AleaSim.CLI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AleaSim.Domain.Entities;
using AleaSim.Domain.Enums;

class Program {
    private static IServiceProvider _serviceProvider = null!;
    private static Guid? _currentUserId = null;
    private static Guid? _currentSessionId = null;
    private static string _currentGame = "slot";

    static async Task Main(string[] args) {
        Console.Title = "AleaSim CLI - Headless Client";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Initializing AleaSim Behavioral Engine...");
        Console.ResetColor();

        // 1. Setup Configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true) // Will fallback to hardcoded if missing
            .Build();

        // 2. Setup Services
        var services = new ServiceCollection();
        
        // Logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // DB (Use Same Connection as API for Integration Testing)
        string connString = "Server=localhost;Port=5894;Database=aleasim;User=root;Password=AleaSim;";
        services.AddDbContext<AleaSimDbContext>(options => 
            options.UseMySql(connString, ServerVersion.AutoDetect(connString)));

        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddSingleton<IRealTimeService, ConsoleRealTimeService>();
        
        // Add Domain Core
        services.AddAleaSimCore();

        _serviceProvider = services.BuildServiceProvider();

        // 3. Ensure DB Connection
        using (var scope = _serviceProvider.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
            try {
                if (await db.Database.CanConnectAsync()) {
                    Console.WriteLine("Connected to Database.");
                } else {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Cannot connect to Database. Ensure Docker is running.");
                    Console.ResetColor();
                    return;
                }
            } catch (Exception ex) {
                Console.WriteLine($"DB Error: {ex.Message}");
                return;
            }
        }

        // 4. Command Loop
        while (true) {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(_currentUserId == null ? "> " : $"[{_currentGame}] > ");
            Console.ResetColor();
            
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            try {
                await ProcessCommand(input.Trim().Split(' '));
            } catch (Exception ex) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    static async Task ProcessCommand(string[] parts) {
        string cmd = parts[0].ToLower();

        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var director = scope.ServiceProvider.GetRequiredService<IGameDirector>();
        var vault = scope.ServiceProvider.GetRequiredService<IVaultService>();
        var brain = scope.ServiceProvider.GetRequiredService<IBrainService>();

        switch (cmd) {
            case "help":
                Console.WriteLine("Commands: login <username>, register <user> <email>, balance, spin <amt>, game <name>, auto <count> <amt>, profile, exit");
                break;

            case "exit":
                Environment.Exit(0);
                break;

            case "register":
                if (parts.Length < 3) { Console.WriteLine("Usage: register <username> <email>"); return; }
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                var newUser = new User {
                    Id = Guid.NewGuid(),
                    Username = parts[1],
                    Email = parts[2],
                    PasswordHash = hasher.HashPassword("password"),
                    Balance = 1000,
                    Role = Role.User,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                repo.CreateUser(newUser);
                repo.CreatePlayerProfile(new PlayerProfile { Id = Guid.NewGuid(), UserId = newUser.Id, TotalWagered = 0 });
                Console.WriteLine($"User {newUser.Username} created (Balance: 1000).");
                break;

            case "login":
                if (parts.Length < 2) { Console.WriteLine("Usage: login <username>"); return; }
                var user = repo.GetUserByUsername(parts[1]);
                if (user == null) { Console.WriteLine("User not found."); return; }
                _currentUserId = user.Id;
                Console.WriteLine($"Logged in as {user.Username}.");
                _currentSessionId = null; // Reset session
                break;

            case "game":
                if (parts.Length < 2) { Console.WriteLine("Usage: game <slot|roulette|blackjack>"); return; }
                _currentGame = parts[1].ToLower();
                _currentSessionId = null;
                Console.WriteLine($"Switched to {_currentGame}");
                break;

            case "spin":
            case "bet":
                if (_currentUserId == null) { Console.WriteLine("Please login first."); return; }
                decimal amount = parts.Length > 1 ? decimal.Parse(parts[1]) : 1.0m;
                
                if (_currentSessionId == null) {
                    var session = await director.StartSession(_currentGame, _currentUserId.Value);
                    _currentSessionId = session.Id;
                    Console.WriteLine($"Started new session: {_currentSessionId}");
                }

                Console.WriteLine($"Spinning {_currentGame} with {amount:C}...");
                var round = await director.PlayRound(_currentGame, _currentUserId.Value, _currentSessionId.Value, amount, new { });
                
                Console.WriteLine($"Result: {round.DecisionType}");
                if (round.TotalWinAmount > 0) {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"WIN: {round.TotalWinAmount:C} ({round.TotalWinAmount/amount:F1}x)");
                    Console.ResetColor();
                } else {
                    Console.WriteLine("Loss.");
                }
                
                // Show Brain Directive
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Brain Decision: {round.DecisionType}");
                Console.ResetColor();
                break;

            case "auto":
                if (_currentUserId == null) { Console.WriteLine("Please login first."); return; }
                int count = int.Parse(parts[1]);
                decimal bet = parts.Length > 2 ? decimal.Parse(parts[2]) : 1.0m;
                
                if (_currentSessionId == null) {
                    var session = await director.StartSession(_currentGame, _currentUserId.Value);
                    _currentSessionId = session.Id;
                }

                Console.WriteLine($"Running {count} spins...");
                decimal totalIn = 0;
                decimal totalOut = 0;

                for(int i=0; i<count; i++) {
                    try {
                        var res = await director.PlayRound(_currentGame, _currentUserId.Value, _currentSessionId.Value, bet, new { });
                        totalIn += res.TotalBetAmount;
                        totalOut += res.TotalWinAmount;
                        if (i % 10 == 0) Console.Write(".");
                    } catch (Exception ex) {
                        Console.WriteLine($"Error on spin {i}: {ex.Message}");
                        break;
                    }
                }
                Console.WriteLine("\nDone.");
                Console.WriteLine($"RTP for run: {(totalIn > 0 ? (totalOut/totalIn)*100 : 0):F2}%");
                break;

            case "balance":
                if (_currentUserId == null) return;
                var u = repo.GetUser(_currentUserId.Value);
                Console.WriteLine($"Balance: {u?.Balance:C} | Bonus: {u?.BonusBalance:C}");
                break;
                
            case "profile":
                if (_currentUserId == null) return;
                var p = repo.GetPlayerProfile(_currentUserId.Value);
                if (p == null) { Console.WriteLine("No profile."); return; }
                Console.WriteLine($"LTV: {p.TotalWagered - p.TotalPaid:C}");
                Console.WriteLine($"Actual RTP: {p.ActualRtp*100:F2}%");
                Console.WriteLine($"Loss Streak: {p.LossStreak}");
                Console.WriteLine($"Avg Spin Time: {p.AvgSpinInterval:F2}s");
                break;

            default:
                Console.WriteLine("Unknown command.");
                break;
        }
    }
}