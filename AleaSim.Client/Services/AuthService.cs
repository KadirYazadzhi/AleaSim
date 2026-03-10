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
                if (result.RequiresTwoFactor) return result;

                await StoreAuthResult(result);
                return result;
            }
        }
        
        throw new Exception("Login failed. Check credentials.");
    }

    public async Task<LoginResponse> Login2FA(TwoFactorLoginRequest request) {
        var response = await _httpClient.PostAsJsonAsync("api/Auth/login/2fa", request);
        
        if (response.IsSuccessStatusCode) {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result != null) {
                await StoreAuthResult(result);
                return result;
            }
        }
        
        var error = await response.Content.ReadAsStringAsync();
        throw new Exception(string.IsNullOrEmpty(error) ? "Invalid 2FA code." : error);
    }

    private async Task StoreAuthResult(LoginResponse result) {
        await _localStorage.SetItemAsync("authToken", result.Token);
        ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
    }

    public async Task<string> RefreshToken() {
        var token = await _localStorage.GetItemAsync<string>("authToken");

        var response = await _httpClient.PostAsJsonAsync("api/Auth/refresh", new RefreshTokenRequest { 
            Token = token
        });

        if (!response.IsSuccessStatusCode) {
            await Logout();
            return string.Empty;
        }

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result == null) {
            await Logout();
            return string.Empty;
        }

        await _localStorage.SetItemAsync("authToken", result.Token);

        return result.Token;
    }

    public async Task Register(RegisterRequest request) {
        var response = await _httpClient.PostAsJsonAsync("api/Auth/register", request);
        if (!response.IsSuccessStatusCode) {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception(error);
        }
    }

    public async Task Logout() {
        await _httpClient.PostAsync("api/Auth/logout", null); // Notify server to clear cookie and revoke session
        await _localStorage.RemoveItemAsync("authToken");
        ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
    }

    public async Task<UserDto?> GetMe() {
        try {
            var response = await _httpClient.GetAsync("api/Auth/me");
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) {
                await Logout();
                return null;
            }
            if (response.IsSuccessStatusCode) {
                return await response.Content.ReadFromJsonAsync<UserDto>();
            }
            return null;
        } catch {
            return null;
        }
    }

    public async Task<string> GetAvatar() {
        try {
            var response = await _httpClient.GetAsync("api/Auth/avatar");
            if (response.IsSuccessStatusCode) {
                return await response.Content.ReadAsStringAsync();
            }
            return string.Empty;
        } catch {
            return string.Empty;
        }
    }
}
