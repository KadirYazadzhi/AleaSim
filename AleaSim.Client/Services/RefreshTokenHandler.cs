using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using AleaSim.Client.Services;
using Blazored.LocalStorage;
using Microsoft.Extensions.DependencyInjection;

namespace AleaSim.Client;

public class RefreshTokenHandler : DelegatingHandler {
    private readonly IServiceProvider _services;
    private readonly NavigationManager _navigationManager;
    private readonly ILocalStorageService _localStorage;
    private bool _isRefreshing = false;

    public RefreshTokenHandler(IServiceProvider services, NavigationManager navigationManager, ILocalStorageService localStorage) {
        _services = services;
        _navigationManager = navigationManager;
        _localStorage = localStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        // Skip adding authorization header for login/register/refresh endpoints
        var absPath = request.RequestUri?.AbsolutePath ?? "";
        if (!absPath.Contains("api/Auth/login") && 
            !absPath.Contains("api/Auth/register") && 
            !absPath.Contains("api/Auth/refresh")) {
            
            var token = await _localStorage.GetItemAsync<string>("authToken");
            if (!string.IsNullOrEmpty(token)) {
                request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !_isRefreshing) {
            _isRefreshing = true;
            
            try {
                // Resolve scoped service inside the handler
                var authService = _services.GetRequiredService<IAuthService>();
                var newToken = await authService.RefreshToken();

                if (!string.IsNullOrEmpty(newToken)) {
                    // Retry original request with new token
                    request.Headers.Authorization = new AuthenticationHeaderValue("bearer", newToken);
                    response = await base.SendAsync(request, cancellationToken);
                }
            }
            catch {
                // Let the UI handle unauthorized access if it's a protected page
            }
            finally {
                _isRefreshing = false;
            }
        }

        return response;
    }
}
