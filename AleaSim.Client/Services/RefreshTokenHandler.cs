using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using AleaSim.Client.Services;
using Blazored.LocalStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;

namespace AleaSim.Client;

public class RefreshTokenHandler : DelegatingHandler {
    private readonly IServiceProvider _services;
    private readonly NavigationManager _navigationManager;
    private readonly ILocalStorageService _localStorage;
    
    // Static task to handle simultaneous refresh requests across transient handlers
    private static Task<string>? _refreshTask = null;
    private static readonly object _refreshLock = new();

    public RefreshTokenHandler(IServiceProvider services, NavigationManager navigationManager, ILocalStorageService localStorage) {
        _services = services;
        _navigationManager = navigationManager;
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        var absPath = request.RequestUri?.AbsolutePath ?? "";
        bool isAuthEndpoint = absPath.Contains("api/Auth/login") || 
                             absPath.Contains("api/Auth/register") || 
                             absPath.Contains("api/Auth/refresh") ||
                             absPath.Contains("api/Auth/logout");

        var token = await _localStorage.GetItemAsync<string>("authToken");
        if (!isAuthEndpoint && !string.IsNullOrEmpty(token)) {
            request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        // ONLY attempt refresh if we HAD a token and got 401
        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint && !string.IsNullOrEmpty(token)) {
            string newToken = string.Empty;

            Task<string>? localRefreshTask = null;
            lock (_refreshLock) {
                if (_refreshTask == null) {
                    _refreshTask = PerformRefresh();
                }
                localRefreshTask = _refreshTask;
            }

            try {
                newToken = await localRefreshTask;
            } finally {
                lock (_refreshLock) {
                    _refreshTask = null;
                }
            }

            if (!string.IsNullOrEmpty(newToken)) {
                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", newToken);
                response = await base.SendAsync(request, cancellationToken);
            } else {
                // Refresh failed, clear state and go to login
                await _localStorage.RemoveItemAsync("authToken");
                var authProvider = _services.GetRequiredService<AuthenticationStateProvider>() as CustomAuthStateProvider;
                authProvider?.NotifyUserLogout();
                
                // Do NOT use forceLoad: true here as it causes infinite reload loops if initialization logic fails
                if (!Navigation.Uri.Contains("/login")) {
                    _navigationManager.NavigateTo("login?reason=expired");
                }
            }
        }

        return response;
    }

    private async Task<string> PerformRefresh() {
        try {
            var authService = _services.GetRequiredService<IAuthService>();
            return await authService.RefreshToken();
        } catch {
            return string.Empty;
        }
    }
}
