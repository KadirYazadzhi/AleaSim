using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace AleaSim.Api.Services;

public class CustomUserIdProvider : IUserIdProvider {
    public string? GetUserId(HubConnectionContext connection) {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
