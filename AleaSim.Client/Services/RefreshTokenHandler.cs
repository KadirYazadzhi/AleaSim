using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using AleaSim.Client.Services;

namespace AleaSim.Client;

public class RefreshTokenHandler : DelegatingHandler {
    private readonly IServiceProvider _services;
    private bool _isRefreshing = false;

    public RefreshTokenHandler(IServiceProvider services) {
        _services = services;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !_isRefreshing) {
            _isRefreshing = true;
            
            try {
                // Resolve scoped service inside the handler
                var authService = _services.GetRequiredService<IAuthService>();
                var newToken = await authService.RefreshToken();

                if (!string.IsNullOrEmpty(newToken)) {
                    request.Headers.Authorization = new AuthenticationHeaderValue("bearer", newToken);
                    response = await base.SendAsync(request, cancellationToken);
                }
            }
            finally {
                _isRefreshing = false;
            }
        }

        return response;
    }
}
