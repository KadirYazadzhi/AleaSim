using System.Net;
using System.Text.Json;

namespace AleaSim.Api.Middleware;

public class ExceptionHandlingMiddleware {
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger) {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context) {
        try {
            await _next(context);
        } catch (Exception ex) {
            _logger.LogError(ex, "An unhandled exception occurred.");
            await HandleExceptionAsync(context, ex);
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
