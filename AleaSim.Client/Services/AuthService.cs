using AleaSim.Shared.Models;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AleaSim.Client.Services;

public class AuthService : IAuthService {
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;

    public AuthService(HttpClient httpClient, ILocalStorageService localStorage, AuthenticationStateProvider authStateProvider) {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<LoginResponse> Login(LoginRequest request) {
        var response = await _httpClient.PostAsJsonAsync("api/Auth/login", request);
        
        if (response.IsSuccessStatusCode) {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result != null) {
                await _localStorage.SetItemAsync("authToken", result.Token);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", result.Token);
                return result;
            }
        }
        
        throw new Exception("Login failed. Check credentials.");
    }

    public async Task Register(RegisterRequest request) {
        var response = await _httpClient.PostAsJsonAsync("api/Auth/register", request);
        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }

    public async Task Logout() {
        await _localStorage.RemoveItemAsync("authToken");
        ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }
}
