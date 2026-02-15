using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AleaSim.Client;
using MudBlazor.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

using AleaSim.Client.Services; // Added
using System.Globalization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Set Global Culture to en-US for USD currency unification
var culture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient with RefreshTokenHandler
builder.Services.AddTransient<RefreshTokenHandler>();

builder.Services.AddHttpClient("AleaSim.Api", client => {
    client.BaseAddress = new Uri("http://localhost:5286");
})
.AddHttpMessageHandler<RefreshTokenHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AleaSim.Api"));

builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddOptions();
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<AudioService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RealTimeClient>();
builder.Services.AddScoped<LayoutService>();

await builder.Build().RunAsync();
