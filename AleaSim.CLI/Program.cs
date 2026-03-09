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
using System.Text.Json;

class Program {
    private static IServiceProvider _serviceProvider = null!;
    private static Guid? _currentUserId = null;
    private static Guid? _currentSessionId = null;
    private static string _currentGame = "slot";
    private static string _currentMode = "classic";

    static async Task Main(string[] args) {
        Console.Title = "AleaSim CLI - Premium Headless Client";
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║          ALEASIM INTELLIGENT GAMING ENGINE           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.ResetColor();

        // 1. Setup Services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // DB - Use consistent connection string (Adjust if needed for local setup)
        string connString = "Server=localhost;Port=5894;Database=aleasim;User=root;Password=AleaSim;";
        services.AddDbContext<AleaSimDbContext>(options => 
            options.UseMySql(connString, ServerVersion.AutoDetect(connString)));

        services.AddScoped<IGameRepository, EfGameRepository>();
        services.AddSingleton<IRealTimeService, ConsoleRealTimeService>();
        services.AddAleaSimCore();

        _serviceProvider = services.BuildServiceProvider();

        // 2. Initial DB Warmup
        using (var scope = _serviceProvider.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
            try {
                if (await db.Database.CanConnectAsync()) {
                    Console.WriteLine(">>> System Core Connected to Database.");
                } else {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("CRITICAL: Database connection failed.");
                    Console.ResetColor();
                    return;
                }
            } catch (Exception ex) {
                Console.WriteLine($"DB Initialization Error: {ex.Message}");
                return;
            }
        }

        Console.WriteLine("Type 'help' for available commands.");

        // 3. Command Loop
        while (true) {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(_currentUserId == null ? "GUEST > " : $"[{_currentGame.ToUpper()}] > ");
            Console.ResetColor();
            
            string? input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) continue;

            try {
                await ProcessCommand(input.Trim().Split(' '));
            } catch (Exception ex) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: {ex.Message}");
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
        var levelService = scope.ServiceProvider.GetRequiredService<ILevelService>();

        switch (cmd) {
            case "help":
                Console.WriteLine("---------------------------------------------------------");
                Console.WriteLine("GENERAL: register <user> <email>, login <username>, balance, profile, exit");
                Console.WriteLine("GAMING:  game <slot|roulette|blackjack|baccarat|dice>");
                Console.WriteLine("         mode <classic|extreme|slider|multi>");
                Console.WriteLine("         bet <amount> [extra_params]");
                Console.WriteLine("         auto <count> <amount>");
                Console.WriteLine("SYSTEM:  cashback, missions, chat <message>");
                Console.WriteLine("---------------------------------------------------------");
                break;

            case "exit":
                Environment.Exit(0);
                break;

            case "register":
                if (parts.Length < 3) { Console.WriteLine("Usage: register <username> <email>"); return; }
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
                var adminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
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
                repo.CreatePlayerProfile(new PlayerProfile { Id = Guid.NewGuid(), UserId = newUser.Id });
                repo.CreateUserProgression(new UserProgression { Id = Guid.NewGuid(), UserId = newUser.Id });
                Console.WriteLine($"User '{newUser.Username}' registered. Starting balance: $1,000.00");
                break;

            case "login":
                if (parts.Length < 2) { Console.WriteLine("Usage: login <username>"); return; }
                var user = repo.GetUserByUsername(parts[1]);
                if (user == null) { Console.WriteLine("User not found."); return; }
                _currentUserId = user.Id;
                Console.WriteLine($"Authenticated as '{user.Username}'.");
                _currentSessionId = null; 
                break;

            case "game":
                if (parts.Length < 2) { Console.WriteLine("Usage: game <name>"); return; }
                _currentGame = parts[1].ToLower();
                _currentSessionId = null;
                Console.WriteLine($"Active Game Engine: {_currentGame.ToUpper()}");
                break;

            case "mode":
                if (parts.Length < 2) { Console.WriteLine("Usage: mode <name>"); return; }
                _currentMode = parts[1].ToLower();
                Console.WriteLine($"Game Mode set to: {_currentMode.ToUpper()}");
                break;

            case "bet":
            case "spin":
                if (_currentUserId == null) { Console.WriteLine("Action Required: login first."); return; }
                decimal amount = parts.Length > 1 ? decimal.Parse(parts[1]) : 1.0m;
                
                if (_currentSessionId == null) {
                    var session = await director.StartSession(_currentGame, _currentUserId.Value);
                    _currentSessionId = session.Id;
                }

                object betData = new { };
                if (_currentGame == "roulette") {
                    betData = new { Mode = _currentMode, Bets = new[] { new { Type = "color", Value = "red", Amount = amount } } };
                } else if (_currentGame == "baccarat") {
                    betData = new { Type = "Player" };
                } else if (_currentGame == "dice") {
                    if (_currentMode == "slider")
                        betData = new { Mode = "Slider", Type = "Slider", Target = 50.50m, IsOver = true };
                    else
                        betData = new { Mode = "Multi", Numbers = new[] { 6 } };
                }

                Console.WriteLine($"EXECUTING ROUND: {_currentGame.ToUpper()} | BET: {amount:C}");
                var round = await director.PlayRound(_currentGame, _currentUserId.Value, _currentSessionId.Value, amount, betData);
                
                if (round.TotalWinAmount > 0) {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($">>> WIN: {round.TotalWinAmount:C} ({round.TotalWinAmount/amount:F1}x)");
                    Console.ResetColor();
                } else {
                    Console.WriteLine(">>> LOSS.");
                }
                
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Brain Directive: {round.DecisionType} | Reason: {round.DecisionType}");
                Console.ResetColor();
                break;

            case "balance":
                if (_currentUserId == null) return;
                var u = repo.GetUser(_currentUserId.Value);
                Console.WriteLine($"WALLET: Real: {u?.Balance:C} | Bonus: {u?.BonusBalance:C}");
                break;
                
            case "profile":
                if (_currentUserId == null) return;
                var p = repo.GetPlayerProfile(_currentUserId.Value);
                var prog = repo.GetUserProgression(_currentUserId.Value);
                if (p == null) return;
                Console.WriteLine("---------------------------------------------------------");
                Console.WriteLine($"PLAYER: {_currentUserId}");
                Console.WriteLine($"LEVEL: {prog?.CurrentLevel ?? 1} (XP: {prog?.CurrentXP:N0})");
                Console.WriteLine($"LIFETIME RTP: {p.ActualRtp*100:F2}%");
                Console.WriteLine($"TOTAL WAGERED: {p.TotalWagered:C}");
                Console.WriteLine($"TOTAL PAID: {p.TotalPaid:C}");
                Console.WriteLine($"NET P/L: {p.TotalPaid - p.TotalWagered:C}");
                Console.WriteLine($"PENDING CASHBACK: {p.PendingCashback:C}");
                Console.WriteLine("---------------------------------------------------------");
                break;

            case "cashback":
                if (_currentUserId == null) return;
                decimal claimed = await vault.ClaimCashbackAsync(_currentUserId.Value, repo);
                if (claimed > 0) {
                    Console.WriteLine($"Success! Claimed {claimed:C} cashback to your wallet.");
                } else {
                    Console.WriteLine("No pending cashback available.");
                }
                break;

            case "missions":
                if (_currentUserId == null) return;
                var questService = scope.ServiceProvider.GetRequiredService<IQuestService>();
                var quests = await questService.GetActiveQuests(_currentUserId.Value, repo);
                Console.WriteLine("\n--- ACTIVE MISSIONS ---");
                foreach(var q in quests) {
                    string status = q.IsCompleted ? "[DONE]" : $"[{q.CurrentValue:N0}/{q.Quest.TargetValue:N0}]";
                    Console.WriteLine($"{status} {q.Quest.Title} (Reward: {q.Quest.RewardAmount:C})");
                }
                break;

            case "chat":
                if (_currentUserId == null) return;
                var msg = string.Join(" ", parts.Skip(1));
                var userChat = repo.GetUser(_currentUserId.Value);
                if (userChat != null) {
                    var chatMsg = new ChatMessage {
                        Id = Guid.NewGuid(),
                        SenderId = userChat.Id,
                        SenderUsername = userChat.Username,
                        Message = msg,
                        Type = ChatMessageType.Global,
                        Timestamp = DateTime.UtcNow
                    };
                    repo.SaveChatMessage(chatMsg);
                    Console.WriteLine($"[GLOBAL CHAT] You: {msg}");
                }
                break;

            case "auto":
                if (_currentUserId == null) return;
                int count = int.Parse(parts[1]);
                decimal autoBet = parts.Length > 2 ? decimal.Parse(parts[2]) : 1.0m;
                
                if (_currentSessionId == null) {
                    var session = await director.StartSession(_currentGame, _currentUserId.Value);
                    _currentSessionId = session.Id;
                }

                Console.WriteLine($"AUTOPLAY: Running {count} rounds on {_currentGame.ToUpper()}...");
                decimal totalIn = 0; decimal totalOut = 0;

                for(int i=0; i<count; i++) {
                    var res = await director.PlayRound(_currentGame, _currentUserId.Value, _currentSessionId.Value, autoBet, new { });
                    totalIn += res.TotalBetAmount;
                    totalOut += res.TotalWinAmount;
                    if (i % (count/10 == 0 ? 1 : count/10) == 0) Console.Write(".");
                }
                Console.WriteLine($"\nDONE. RTP: {(totalIn > 0 ? (totalOut/totalIn)*100 : 0):F2}%");
                break;

            default:
                Console.WriteLine("Unknown command. Type 'help' for info.");
                break;
        }
    }
}
