using System.IdentityModel.Tokens.Jwt;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Extensions;
using AleaSim.Persistence;
using AleaSim.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Set Global Culture to en-US for USD currency unification
var culture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Add Services
builder.Services.AddControllers();

builder.WebHost.ConfigureKestrel(serverOptions => {
    serverOptions.Limits.MaxRequestBodySize = 1024 * 1024; // 1MB Limit to prevent RAM DoS
});

builder.Services.AddCors(options => {
    options.AddPolicy("DefaultCors", policy => {
        policy.SetIsOriginAllowed(_ => true) 
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSignalR().AddStackExchangeRedis(redisConn); 
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AleaSimDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Persistence Service (Repository)
builder.Services.AddScoped<IGameRepository, EfGameRepository>();

// Real-Time Service (SignalR Implementation)
builder.Services.AddSingleton<IRealTimeService, AleaSim.Api.Services.SignalRRealTimeService>();

// Domain Core (Shared Logic)
builder.Services.AddAleaSimCore();

// Background Workers
builder.Services.AddSingleton<AleaSim.Api.Workers.SentinelBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AleaSim.Api.Workers.SentinelBackgroundService>());
builder.Services.AddHostedService<AleaSim.Api.Workers.RaffleBackgroundService>();
builder.Services.AddHostedService<AleaSim.Api.Workers.DailyBonusBackgroundService>();
builder.Services.AddHostedService<AleaSim.Api.Workers.TournamentPayoutBackgroundService>();
builder.Services.AddHostedService<AleaSim.Api.Workers.AuditWriterBackgroundService>();

// Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing in configuration.");
var key = Encoding.ASCII.GetBytes(jwtKey);
builder.Services.AddAuthentication(x => {
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x => {
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
    
    // SignalR Auth: Extract token from query string
    x.Events = new JwtBearerEvents {
        OnMessageReceived = context => {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/gamehub")) {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = async context => {
            var repo = context.HttpContext.RequestServices.GetRequiredService<IGameRepository>();
            var redis = context.HttpContext.RequestServices.GetRequiredService<AleaSim.Domain.Services.IRedisCacheService>();
            
            // Extract JTI (Session ID)
            var jtiClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti) ?? context.Principal?.FindFirst("jti");
            
            // If JTI is missing, we might be in a legacy session or a dev environment.
            // For production stability, we allow it for now but log warning.
            if (jtiClaim == null || !Guid.TryParse(jtiClaim.Value, out var sessionId)) {
                return; // Let it pass if JTI is missing (Backward compatibility)
            }

            // Check Cache first for session status
            string cacheKey = $"session_active:{sessionId}";
            var isActive = await redis.GetAsync<bool?>(cacheKey);
            
            if (isActive == null) {
                var session = repo.GetUserSession(sessionId);
                // If session record exists, check its status. If missing, we assume it's valid 
                // until the next refresh cycle (to prevent locking out valid legacy users).
                isActive = (session == null) || session.IsActive;
                await redis.SetAsync(cacheKey, isActive.Value, TimeSpan.FromMinutes(5)); 
            }

            if (!isActive.Value) {
                context.Fail("Session revoked");
            }
        }
    };
});

var app = builder.Build();

// Configure Pipeline
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Disabled to fix CORS preflight redirect issue
app.UseMiddleware<AleaSim.Api.Middleware.ExceptionHandlingMiddleware>();
app.UseRouting(); // Explicit routing
app.UseCors("DefaultCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AleaSim.Api.Hubs.GameHub>("/gamehub"); // Added

// Initialize Database & Seed Data
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
    
    try {
        db.Database.Migrate(); // Apply all migrations
    } catch (Exception ex) {
        Console.WriteLine("Migration failed. If the DB is new, try 'dotnet ef database update'. Error: " + ex.Message);
        // Fallback for emergency only
        db.Database.EnsureCreated(); 
    }

    // Seed Games if missing
    List<AleaSim.Domain.Entities.Game> existingGames = new();
    try {
        existingGames = db.Games.ToList();
    } catch {
        Console.WriteLine("Could not read Games table. Schema might be out of sync.");
    }

    void UpdateGame(string type, string name, decimal minBet, decimal maxBet, decimal rtp, Guid? fixedId = null) {
        var g = existingGames.FirstOrDefault(x => x.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
        if (g == null) {
            db.Games.Add(new AleaSim.Domain.Entities.Game { 
                Id = fixedId ?? Guid.NewGuid(), Name = name, Type = type, Provider = "AleaSim Originals", 
                MinBet = minBet, MaxBet = maxBet, TargetRTP = rtp, IsActive = true, PoolBalance = 1000000m 
            });
        } else {
            g.MinBet = minBet;
            g.MaxBet = maxBet;
            g.TargetRTP = rtp;
            db.Games.Update(g);
        }
    }

    UpdateGame("Slot", "Clover Chase", 0.01m, 1000m, 0.95m, Guid.Parse("11111111-1111-1111-1111-111111111111"));
    UpdateGame("Roulette", "Roulette Royale", 0.01m, 100000m, 0.973m);
    UpdateGame("Blackjack", "Blackjack High", 0.01m, 1000m, 0.992m);
    UpdateGame("Baccarat", "Baccarat Royale", 0.01m, 5000m, 0.989m);
    UpdateGame("dice", "Neon Dice", 0.01m, 1000m, 0.99m);
    UpdateGame("fruitblast", "Fruit Blast (Nuclear)", 0.01m, 1000m, 0.968m);
    
    db.SaveChanges();

    // Seed Global Jackpots if missing
    if (!db.Jackpots.Any(j => j.IsGlobal && j.CurrentValue > 0)) {
        // Clear empty/old global jackpots to avoid duplicates with 0 values
        var oldGlobals = db.Jackpots.Where(j => j.IsGlobal).ToList();
        if (oldGlobals.Any()) db.Jackpots.RemoveRange(oldGlobals);

        db.Jackpots.AddRange(
            new AleaSim.Domain.Entities.Jackpot { 
                Id = Guid.NewGuid(), Name = "Global GRAND", Tier = AleaSim.Domain.Entities.JackpotTier.Grand, 
                CurrentValue = 10000m, ContributionRate = 0.002m, IsGlobal = true, LastUpdated = DateTime.UtcNow 
            },
            new AleaSim.Domain.Entities.Jackpot { 
                Id = Guid.NewGuid(), Name = "Global MEGA", Tier = AleaSim.Domain.Entities.JackpotTier.Mega, 
                CurrentValue = 2500m, ContributionRate = 0.0015m, IsGlobal = true, LastUpdated = DateTime.UtcNow 
            },
            new AleaSim.Domain.Entities.Jackpot { 
                Id = Guid.NewGuid(), Name = "Global MAJOR", Tier = AleaSim.Domain.Entities.JackpotTier.Major, 
                CurrentValue = 500m, ContributionRate = 0.001m, IsGlobal = true, MustDropAt = 1000m, LastUpdated = DateTime.UtcNow 
            },
            new AleaSim.Domain.Entities.Jackpot { 
                Id = Guid.NewGuid(), Name = "Global MINI", Tier = AleaSim.Domain.Entities.JackpotTier.Mini, 
                CurrentValue = 50m, ContributionRate = 0.0005m, IsGlobal = true, MustDropAt = 100m, LastUpdated = DateTime.UtcNow 
            }
        );
        db.SaveChanges();
    }

    // SYNC JACKPOTS TO REDIS (Cold Start Fix)
    try {
        var redis = scope.ServiceProvider.GetRequiredService<IRedisService>();
        var rdb = redis.GetDatabase();
        var allJackpots = db.Jackpots.ToList();
        foreach(var j in allJackpots) {
            string jackpotKey = $"jackpot:{j.Id}";
            var exists = rdb.KeyExists(jackpotKey);
            if (!exists) {
                rdb.StringSet(jackpotKey, (double)j.CurrentValue);
            } else {
                // If Redis value is suspiciously low (e.g. < 10% of DB value), force sync from DB
                var rval = (decimal)(double)rdb.StringGet(jackpotKey);
                if (rval < j.CurrentValue * 0.1m) {
                     rdb.StringSet(jackpotKey, (double)j.CurrentValue);
                }
            }
        }
    } catch { /* Redis might not be ready */ }

    // Seed Admin from Configuration
    var adminIdStr = app.Configuration["Admin:Id"] ?? "00000000-0000-0000-0000-000000000001";
    var adminId = Guid.Parse(adminIdStr);
    var adminInitialPassword = app.Configuration["Admin:InitialPassword"] ?? "admin";
    
    var admin = db.Users.FirstOrDefault(u => u.Role == AleaSim.Domain.Enums.Role.Admin);
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (admin == null) {
        admin = new AleaSim.Domain.Entities.User {
            Id = adminId,
            Username = "admin",
            PasswordHash = hasher.HashPassword(adminInitialPassword), 
            Email = "admin@aleasim.com",
            Role = AleaSim.Domain.Enums.Role.Admin,
            Balance = 1000000m,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.Users.Add(admin);
    } 

    // Ensure associated records exist
    if (!db.UserProgressions.Any(p => p.UserId == adminId)) {
        db.UserProgressions.Add(new AleaSim.Domain.Entities.UserProgression { UserId = adminId });
    }
    if (!db.PlayerProfiles.Any(p => p.UserId == adminId)) {
        db.PlayerProfiles.Add(new AleaSim.Domain.Entities.PlayerProfile { UserId = adminId });
    }
    
    db.SaveChanges();
}

app.Run();