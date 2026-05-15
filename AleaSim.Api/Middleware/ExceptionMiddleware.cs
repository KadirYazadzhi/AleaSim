using System.Net;
using System.Text.Json;
using AleaSim.Domain.Entities;
using AleaSim.Persistence;
using System.Security.Claims;

namespace AleaSim.Api.Middleware;

public class ExceptionHandlingMiddleware {
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IServiceScopeFactory scopeFactory) {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context) {
        try {
            await _next(context);
        } catch (UnauthorizedAccessException ex) {
            _logger.LogWarning("Unauthorized access attempt: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        } catch (Exception ex) {
            _logger.LogError(ex, "An unhandled exception occurred.");
            await LogErrorToDb(context, ex);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task LogErrorToDb(HttpContext context, Exception ex) {
        try {
            // Check if exception is related to missing tables during startup
            if (ex.Message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)) {
                return; // Silently skip DB logging for schema errors to avoid infinite loops/spam
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AleaSimDbContext>();
            
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            var error = new SystemError {
                Id = Guid.NewGuid(),
                Message = ex.Message,
                StackTrace = ex.StackTrace ?? "",
                Source = ex.Source ?? "",
                Path = context.Request.Path,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            
            db.SystemErrors.Add(error);
            await db.SaveChangesAsync();
        } catch (Exception dbEx) {
            _logger.LogCritical(dbEx, "CRITICAL: Failed to log error to database!");
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception) {
        context.Response.ContentType = "application/json";
        
        var statusCode = HttpStatusCode.InternalServerError;
        var message = "An internal error occurred. Please try again later.";

        if (exception is UnauthorizedAccessException) {
            statusCode = HttpStatusCode.Unauthorized;
            message = exception.Message;
        }

        context.Response.StatusCode = (int)statusCode;

        var response = new {
            error = exception.Message,
            detail = message
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
