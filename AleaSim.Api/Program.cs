using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add Services
builder.Services.AddControllers();
builder.Services.AddSignalR(); // Added SignalR
builder.Services.AddCors(options => {
    options.AddPolicy("AllowBlazor",
        policy => policy
            .WithOrigins("https://localhost:7076", "http://localhost:5286")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
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

// Domain Services (Singleton for simulation state)
builder.Services.AddSingleton<IRngService, DeterministicRngService>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IRealTimeService, AleaSim.Api.Services.SignalRRealTimeService>(); // Added
builder.Services.AddSingleton<IRtpEngine, RtpEngine>();
builder.Services.AddSingleton<IVaultService, VaultService>(); // New Financial Core
builder.Services.AddSingleton<IBrainService, BrainService>(); // New Intelligence Core
builder.Services.AddScoped<IQuestService, QuestService>(); // New Quest System
builder.Services.AddScoped<IAchievementService, AchievementService>(); // New Achievement System
builder.Services.AddScoped<IVoucherService, VoucherService>(); // New Voucher System
builder.Services.AddScoped<ITournamentService, TournamentService>(); // New Tournament System
builder.Services.AddScoped<ILevelService, LevelService>(); // New RPG System
builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>(); // New Social System
builder.Services.AddSingleton<IPromotionService, PromotionService>(); // New Promotions
builder.Services.AddSingleton<IJackpotService, JackpotService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddSingleton<IAuditService, AuditService>();
 // Audit is singleton to manage hash chain in memory? Or Scoped?
// AuditService implementation uses IServiceScopeFactory, so it can be Singleton.
builder.Services.AddScoped<IGameDirector, GameDirector>(); // Added Director (Scoped because it uses Repo)

// Background Workers
builder.Services.AddSingleton<AleaSim.Api.Workers.SentinelBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AleaSim.Api.Workers.SentinelBackgroundService>());
builder.Services.AddHostedService<AleaSim.Api.Workers.RaffleBackgroundService>();
builder.Services.AddHostedService<AleaSim.Api.Workers.DailyBonusBackgroundService>();

// Game Engines
builder.Services.AddSingleton<SlotGameEngine>();
builder.Services.AddSingleton<RouletteGameEngine>();
builder.Services.AddSingleton<BlackjackGameEngine>();

// Game Factory / Resolver
builder.Services.AddSingleton<Func<string, IGame>>(serviceProvider => key => {
    return key.ToLower() switch {
        "slot" => serviceProvider.GetRequiredService<SlotGameEngine>(),
        "roulette" => serviceProvider.GetRequiredService<RouletteGameEngine>(),
        "blackjack" => serviceProvider.GetRequiredService<BlackjackGameEngine>(),
        _ => throw new KeyNotFoundException("Game engine not found")
    };
});

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
});

var app = builder.Build();

// Configure Pipeline
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // Disabled to fix CORS preflight redirect issue
app.UseCors("AllowBlazor");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AleaSim.Api.Hubs.GameHub>("/gamehub"); // Added

// Initialize Database & Seed Data
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
    
    // For Development/Demo: Reset Database to apply new schema (PlayerProfiles, TournamentEntries)
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated(); // Auto-create tables if missing

    // Seed Games if missing
    if (!db.Games.Any()) {
        db.Games.AddRange(
            new AleaSim.Domain.Entities.Game { Id = Guid.NewGuid(), Name = "Slot Machine", Type = "Slot", MinBet = 1, MaxBet = 100, TargetRTP = 0.95, IsActive = true },
            new AleaSim.Domain.Entities.Game { Id = Guid.NewGuid(), Name = "European Roulette", Type = "Roulette", MinBet = 1, MaxBet = 500, TargetRTP = 0.97, IsActive = true },
            new AleaSim.Domain.Entities.Game { Id = Guid.NewGuid(), Name = "Blackjack", Type = "Blackjack", MinBet = 5, MaxBet = 200, TargetRTP = 0.99, IsActive = true }
        );
        db.SaveChanges();
    }

    // Seed Admin if missing
    if (!db.Users.Any(u => u.Username == "admin")) {
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        db.Users.Add(new AleaSim.Domain.Entities.User {
            Id = Guid.NewGuid(),
            Username = "admin",
            PasswordHash = hasher.HashPassword("admin"), // Hashed password
            Email = "admin@aleasim.com",
            Role = AleaSim.Domain.Enums.Role.Admin,
            Balance = 1000000m,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        db.SaveChanges();
    }
}

app.Run();