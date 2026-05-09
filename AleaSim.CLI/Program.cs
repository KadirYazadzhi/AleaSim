using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Extensions;
using AleaSim.Persistence;
using AleaSim.CLI.Services;
using AleaSim.Shared.Models;
using System.Diagnostics;
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

        // 1. Setup Configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("../AleaSim.Api/appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=localhost;Port=3306;Database=aleasim;User=root;Password=secretpassword;";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(configuration);

        services.AddDbContext<AleaSimDbContext>(options => {
            try {
                options.UseMySql(connString, ServerVersion.AutoDetect(connString));
            } catch {
                // Fallback to a default version if DB is not reachable during startup
                options.UseMySql(connString, new MySqlServerVersion(new Version(8, 0, 31)));
            }
        });

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
                Console.WriteLine("GAMING:  game <slot|fruitblast|roulette|blackjack|baccarat|dice>");
                Console.WriteLine("         mode <classic|extreme|slider|multi>");
                Console.WriteLine("         bet <amount> [extra_params]");
                Console.WriteLine("         auto <count> <amount>");
                Console.WriteLine("SYSTEM:  cashback, missions, chat <message>, chat history");
                Console.WriteLine("         leaderboard, jackpots, livewinners");
                Console.WriteLine("         redeem <code>");
                Console.WriteLine("         pchat <username> <message>, pchat history <username>");
                Console.WriteLine("ADMIN:   admin sim <game> <count> <bet>");
                Console.WriteLine("         admin stats");
                Console.WriteLine("         admin rtp <target>");
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
                if (parts.Length > 1 && parts[1].ToLower() == "history") {
                    var history = repo.GetGlobalChatMessages(50);
                    Console.WriteLine("\n--- GLOBAL CHAT HISTORY ---");
                    foreach (var m in history) {
                        Console.WriteLine($"[{m.Timestamp:HH:mm}] {m.SenderUsername}: {m.Message}");
                    }
                    return;
                }
                var msg = string.Join(" ", parts.Skip(1));
                var userChat = repo.GetUser(_currentUserId.Value);
                if (userChat != null) {
                    var chatMsg = new ChatMessage {
                        Id = Guid.NewGuid(),
                        SenderId = userChat.Id,
                        SenderUsername = userChat.Username,
                        SenderAvatarUrl = userChat.AvatarUrl ?? "",
                        Message = msg,
                        Type = ChatMessageType.Global,
                        Timestamp = DateTime.UtcNow
                    };
                    repo.SaveChatMessage(chatMsg);
                    Console.WriteLine($"[GLOBAL CHAT] You: {msg}");
                }
                break;

            case "pchat":
                if (_currentUserId == null) return;
                if (parts.Length < 2) { Console.WriteLine("Usage: pchat <username> <message> OR pchat history <username>"); return; }
                
                string targetName = parts[1];
                var me = repo.GetUser(_currentUserId.Value);
                if (me == null) return;

                if (targetName.ToLower() == "history" && parts.Length >= 3) {
                    string otherName = parts[2];
                    var otherUser = repo.GetUserByUsername(otherName);
                    if (otherUser == null) { Console.WriteLine("User not found."); return; }
                    var pHistory = repo.GetPrivateChatHistory(me.Id, otherUser.Id, 50);
                    Console.WriteLine($"\n--- PRIVATE HISTORY WITH {otherName.ToUpper()} ---");
                    foreach (var m in pHistory) {
                        string prefix = m.SenderId == me.Id ? "You" : m.SenderUsername;
                        Console.WriteLine($"[{m.Timestamp:HH:mm}] {prefix}: {m.Message}");
                    }
                    return;
                }

                // Sending message
                if (parts.Length < 3) { Console.WriteLine("Usage: pchat <username> <message>"); return; }
                var target = repo.GetUserByUsername(targetName);
                if (target == null) { Console.WriteLine("Target user not found."); return; }

                // Check authorization (Admin required for private chat)
                if (me.Role != Role.Admin && target.Role != Role.Admin) {
                    Console.WriteLine("Private chat is only available with Administrators.");
                    return;
                }

                string pMsg = string.Join(" ", parts.Skip(2));
                var pChatMsg = new ChatMessage {
                    Id = Guid.NewGuid(),
                    SenderId = me.Id,
                    SenderUsername = me.Username,
                    SenderAvatarUrl = me.AvatarUrl ?? "",
                    ReceiverId = target.Id,
                    Message = pMsg,
                    Type = ChatMessageType.Private,
                    Timestamp = DateTime.UtcNow
                };
                repo.SaveChatMessage(pChatMsg);
                
                var realTime = scope.ServiceProvider.GetRequiredService<IRealTimeService>();
                await realTime.NotifyPrivateMessage(me.Id, target.Id, me.Username, pMsg, me.AvatarUrl ?? "", pChatMsg.Id);
                Console.WriteLine($"[PRIVATE to {target.Username}] You: {pMsg}");
                break;

            case "redeem":
                if (_currentUserId == null) return;
                if (parts.Length < 2) { Console.WriteLine("Usage: redeem <code>"); return; }
                var vService = scope.ServiceProvider.GetRequiredService<IVoucherService>();
                try {
                    decimal vAmount = await vService.RedeemVoucher(_currentUserId.Value, parts[1], repo, vault);
                    Console.WriteLine($"Success! Voucher redeemed for {vAmount:C}");
                } catch (Exception ex) {
                    Console.WriteLine($"Redemption Failed: {ex.Message}");
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

            case "leaderboard":
                var leaders = repo.GetTopWinners(DateTime.UtcNow, 10);
                Console.WriteLine("\n--- GLOBAL LEADERBOARD ---");
                foreach (var l in leaders) {
                    Console.WriteLine($"{l.Username.PadRight(15)} | {l.TotalWin:C}");
                }
                break;

            case "jackpots":
                var jpts = repo.GetJackpots();
                Console.WriteLine("\n--- CURRENT PROGRESSIVE JACKPOTS ---");
                foreach (var j in jpts) {
                    string mustDrop = j.MustDropAt.HasValue ? $" | Must Drop: {j.MustDropAt:C}" : "";
                    Console.WriteLine($"{j.Tier.ToString().PadRight(10)} | {j.CurrentValue:C}{mustDrop} | Global: {j.IsGlobal}");
                }
                break;
                
            case "livewinners":
                var rounds = repo.GetGlobalHistory(10);
                Console.WriteLine("\n--- LIVE WINNERS ---");
                foreach(var r in rounds.Where(x => x.WinAmount > 0)) {
                    Console.WriteLine($"[{r.GameName.ToUpper()}] Won {r.WinAmount:C} (Bet: {r.BetAmount:C}) | {r.ResultSummary}");
                }
                break;

            case "admin":
                if (parts.Length < 2) { Console.WriteLine("Usage: admin <sim|stats|rtp|rtp-verify>"); return; }
                string adminCmd = parts[1].ToLower();
                
                if (adminCmd == "stats") {
                    var stats = repo.GetStatsForPeriod(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
                    Console.WriteLine("\n--- PLATFORM STATS (30 DAYS) ---");
                    Console.WriteLine($"Total Bets:  {stats.TotalBets:C}");
                    Console.WriteLine($"Total Wins:  {stats.TotalWins:C}");
                    Console.WriteLine($"Total GGR:   {stats.Ggr:C}");
                    Console.WriteLine($"Global RTP:  {stats.CurrentRtp:F2}%");
                } 
                else if (adminCmd == "rtp") {
                    if (parts.Length < 3) { Console.WriteLine("Usage: admin rtp <target>"); return; }
                    repo.SetGlobalSetting("GlobalTargetRtp", parts[2]);
                    Console.WriteLine($"Global Target RTP set to {parts[2]}%");
                }
                else if (adminCmd == "rtp-verify") {
                    if (parts.Length < 4) { Console.WriteLine("Usage: admin rtp-verify <game> <iterations>"); return; }
                    string game = parts[2];
                    int totalIters = int.Parse(parts[3]);
                    int threads = Environment.ProcessorCount;
                    int itersPerThread = totalIters / threads;
                    
                    Console.WriteLine($"\n[MATH VALIDATION] Verifying {game} RTP over {totalIters:N0} rounds using {threads} threads...");
                    var simService = scope.ServiceProvider.GetRequiredService<ISimulationService>();
                    var tasks = new List<Task<SimulationReport>>();
                    var sw = Stopwatch.StartNew();

                    for (int i = 0; i < threads; i++) {
                        tasks.Add(simService.RunSimulation(new SimulationRequest { 
                            GameType = game, 
                            Iterations = itersPerThread, 
                            BetAmount = 1.0m 
                        }));
                    }

                    var results = await Task.WhenAll(tasks);
                    sw.Stop();

                    decimal combinedBet = results.Sum(r => r.TotalBet);
                    decimal combinedWin = results.Sum(r => r.TotalWin);
                    decimal finalRtp = (combinedWin / combinedBet) * 100;

                    Console.WriteLine("\n--- RTP VALIDATION REPORT ---");
                    Console.WriteLine($"Duration:    {sw.Elapsed.TotalSeconds:F2}s");
                    Console.WriteLine($"Throughput:  {totalIters / sw.Elapsed.TotalSeconds:N0} rounds/sec");
                    Console.WriteLine($"Total Bet:   {combinedBet:C}");
                    Console.WriteLine($"Total Win:   {combinedWin:C}");
                    Console.WriteLine($"FINAL RTP:   {finalRtp:F4}%");
                    
                    if (Math.Abs(finalRtp - 95.0m) > 5.0m) {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("WARNING: Significant deviation from target RTP (95%)!");
                        Console.ResetColor();
                    } else {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("SUCCESS: Math model within tolerance.");
                        Console.ResetColor();
                    }
                }
                else if (adminCmd == "sim") {
                    if (parts.Length < 5) { Console.WriteLine("Usage: admin sim <game> <count> <bet>"); return; }
                    string simGame = parts[2];
                    int simCount = int.Parse(parts[3]);
                    decimal simBet = decimal.Parse(parts[4]);
                    
                    Console.WriteLine($"ADMIN SIMULATION: {simCount} rounds on {simGame} at {simBet:C}...");
                    var simUser = new User { Id = Guid.NewGuid(), Username = "Sim_" + Guid.NewGuid().ToString().Substring(0,8), Role = Role.User, Balance = 1000000 };
                    repo.CreateUser(simUser);
                    repo.CreatePlayerProfile(new PlayerProfile { Id = Guid.NewGuid(), UserId = simUser.Id, TotalWagered = 1000000 });
                    
                    var simSession = await director.StartSession(simGame, simUser.Id);
                    decimal simIn = 0; decimal simOut = 0;

                    for(int i=0; i<simCount; i++) {
                        var res = await director.PlayRound(simGame, simUser.Id, simSession.Id, simBet, new { });
                        simIn += res.TotalBetAmount;
                        simOut += res.TotalWinAmount;
                        if (i % (simCount/10 == 0 ? 1 : simCount/10) == 0) Console.Write(".");
                    }
                    Console.WriteLine($"\nSIM DONE. RTP: {(simIn > 0 ? (simOut/simIn)*100 : 0):F2}%");
                    Console.WriteLine($"Total Bet: {simIn:C} | Total Win: {simOut:C}");
                }
                break;

            default:
                Console.WriteLine("Unknown command. Type 'help' for info.");
                break;
        }
    }
}
