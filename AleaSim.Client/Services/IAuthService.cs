using AleaSim.Shared.Models;

namespace AleaSim.Client.Services;

public interface IAuthService {
    Task<LoginResponse> Login(LoginRequest request);
    Task Register(RegisterRequest request);
    Task Logout();
}
