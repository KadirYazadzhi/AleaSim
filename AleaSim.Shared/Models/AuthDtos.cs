namespace AleaSim.Shared.Models;

public class LoginRequest {
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    
    public LoginRequest() {}
    public LoginRequest(string username, string password) {
        Username = username;
        Password = password;
    }
}

public class LoginResponse {
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public LoginResponse() {}
    public LoginResponse(string token, string refreshToken, string username, string role) {
        Token = token;
        RefreshToken = refreshToken;
        Username = username;
        Role = role;
    }
}

public class RefreshTokenRequest {
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class RegisterRequest {
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public RegisterRequest() {}
    public RegisterRequest(string username, string password, string email) {
        Username = username;
        Password = password;
        Email = email;
    }
}
