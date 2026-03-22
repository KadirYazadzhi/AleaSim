using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AleaSim.Api.Controllers;

public abstract class BaseApiController : ControllerBase {
    protected Guid GetUserIdOrThrow() {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value 
            ?? User.FindFirst("userId")?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId)) {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        
        return userId;
    }
}
