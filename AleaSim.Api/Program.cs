using System.IdentityModel.Tokens.Jwt;
using AleaSim.Domain.Interfaces;
using AleaSim.Domain.Services;
using AleaSim.Domain.Extensions;
using AleaSim.Persistence;
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
builder.Services.AddSignalR(); // Added SignalR
builder.Services.AddCors(options => {
    options.AddPolicy("AllowBlazor",
        policy => policy
            .SetIsOriginAllowed(_ => true) // Allow any origin for Dev (fixes localhost http vs https mismatches)
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

// Real-Time Service (SignalR Implementation)
builder.Services.AddSingleton<IRealTimeService, AleaSim.Api.Services.SignalRRealTimeService>();

// Domain Core (Shared Logic)
builder.Services.AddAleaSimCore();

// Background Workers
builder.Services.AddSingleton<AleaSim.Api.Workers.SentinelBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AleaSim.Api.Workers.SentinelBackgroundService>());
builder.Services.AddHostedService<AleaSim.Api.Workers.RaffleBackgroundService>();
builder.Services.AddHostedService<AleaSim.Api.Workers.DailyBonusBackgroundService>();

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
app.UseCors("AllowBlazor");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AleaSim.Api.Hubs.GameHub>("/gamehub"); // Added

// Initialize Database & Seed Data
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
    
    db.Database.EnsureCreated(); // Auto-create tables if missing
    // Seed Games if missing
    if (!db.Games.Any()) {
        db.Games.AddRange(
            new AleaSim.Domain.Entities.Game { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Slot Machine", Type = "Slot", Provider = "AleaSim Originals", MinBet = 1, MaxBet = 1000, TargetRTP = 0.95m, IsActive = true, PoolBalance = 1000000m },
            new AleaSim.Domain.Entities.Game { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "European Roulette", Type = "Roulette", Provider = "AleaSim Originals", MinBet = 1, MaxBet = 100000, TargetRTP = 0.97m, IsActive = true, PoolBalance = 1000000m },
            new AleaSim.Domain.Entities.Game { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Blackjack", Type = "Blackjack", Provider = "AleaSim Originals", MinBet = 5, MaxBet = 1000, TargetRTP = 0.99m, IsActive = true }
        );
        db.SaveChanges();
    }

    // Seed Global Jackpots if missing
    if (!db.Jackpots.Any(j => j.IsGlobal)) {
        db.Jackpots.AddRange(
            new AleaSim.Domain.Entities.Jackpot { 
                Id = Guid.NewGuid(), Name = "Global MEGA Spades", Tier = AleaSim.Domain.Entities.JackpotTier.Spades, 
                CurrentValue = 10000m, ContributionRate = 0.002m, IsGlobal = true, LastUpdated = DateTime.UtcNow 
            },
            new AleaSim.Domain.Entities.Jackpot { 
                Id = Guid.NewGuid(), Name = "Global MAJOR Hearts", Tier = AleaSim.Domain.Entities.JackpotTier.Hearts, 
                CurrentValue = 2500m, ContributionRate = 0.0015m, IsGlobal = true, LastUpdated = DateTime.UtcNow 
            },
            new AleaSim.Domain.Entities.Jackpot { 
                Id = Guid.NewGuid(), Name = "Global MINOR Diamonds", Tier = AleaSim.Domain.Entities.JackpotTier.Diamonds, 
                CurrentValue = 500m, ContributionRate = 0.001m, IsGlobal = true, MustDropAt = 1000m, LastUpdated = DateTime.UtcNow 
            },
            new AleaSim.Domain.Entities.Jackpot { 
                Id = Guid.NewGuid(), Name = "Global MINI Clubs", Tier = AleaSim.Domain.Entities.JackpotTier.Clubs, 
                CurrentValue = 50m, ContributionRate = 0.0005m, IsGlobal = true, MustDropAt = 100m, LastUpdated = DateTime.UtcNow 
            }
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