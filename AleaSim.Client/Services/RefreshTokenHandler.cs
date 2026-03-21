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

        // Skip adding authorization header for login/register/refresh endpoints
        var absPath = request.RequestUri?.AbsolutePath ?? "";
        bool isAuthEndpoint = absPath.Contains("api/Auth/login") || 
                             absPath.Contains("api/Auth/register") || 
                             absPath.Contains("api/Auth/refresh") ||
                             absPath.Contains("api/Auth/logout");

        if (!isAuthEndpoint) {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            if (!string.IsNullOrEmpty(token)) {
                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthEndpoint) {
            string newToken = string.Empty;

            // Wait for existing refresh task or start a new one
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
                    // Only clear if we are the ones who finished it (or just let it stay for a moment)
                    // In single-threaded Blazor, this is safer.
                    _refreshTask = null;
                }
            }

            if (!string.IsNullOrEmpty(newToken)) {
                // Retry original request with new token
                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", newToken);
                response = await base.SendAsync(request, cancellationToken);
            } else {
                // Refresh failed, navigate to login and clear state
                await _localStorage.RemoveItemAsync("authToken");
                var authProvider = _services.GetRequiredService<AuthenticationStateProvider>() as CustomAuthStateProvider;
                authProvider?.NotifyUserLogout();
                _navigationManager.NavigateTo("login?reason=expired", forceLoad: true);
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
