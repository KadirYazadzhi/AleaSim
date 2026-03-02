using AleaSim.Shared.Models;

namespace AleaSim.Client.Services;

public interface IAuthService {
    Task<LoginResponse> Login(LoginRequest request);
    Task<LoginResponse> Login2FA(TwoFactorLoginRequest request);
    Task Register(RegisterRequest request);
    Task Logout();
    Task<UserDto?> GetMe();
    Task<string> GetAvatar();
    Task<string> RefreshToken();
}
