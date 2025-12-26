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
builder.Services.AddDbContext<AleaSimDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Domain Services (Singleton for simulation state)
builder.Services.AddSingleton<IRngService, DeterministicRngService>();
builder.Services.AddSingleton<IRtpEngine, RtpEngine>();
builder.Services.AddSingleton<IJackpotService, JackpotService>();
builder.Services.AddSingleton<IAuditService, AuditService>();

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
var key = Encoding.ASCII.GetBytes("ThisIsASecretKeyForAleaSimSimulationProject2025!");
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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();