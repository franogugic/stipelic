using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Api.Middlewares;

public sealed class GlobalExceptionMiddleware
{
    private const string UnexpectedErrorMessage = "An unexpected error occurred.";

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BadRequestException e)
        {
            var statusCode = StatusCodes.Status400BadRequest;
            _logger.LogWarning(e, "Bad request: {Message}", e.Message);
            await HandleExceptionAsync(context, statusCode, e.Message, "BAD_REQUEST");
        }
        catch (UnauthorizedException e)
        {
            var statusCode = StatusCodes.Status401Unauthorized;
            _logger.LogWarning(e, "Unauthorized: {Message}", e.Message);
            await HandleExceptionAsync(context, statusCode, e.Message, "UNAUTHORIZED");
        }
        catch (EmailNotVerifiedException e)
        {
            var statusCode = StatusCodes.Status403Forbidden;
            _logger.LogWarning(e, "Forbidden: {Message}", e.Message);
            await HandleExceptionAsync(context, statusCode, e.Message, "EMAIL_NOT_VERIFIED");
        }
        catch (TooManyLoginAttemptsException e)
        {
            var statusCode = StatusCodes.Status429TooManyRequests;
            _logger.LogWarning(e, "Too many login attempts: {Message}", e.Message);
            await HandleExceptionAsync(context, statusCode, e.Message, "TOO_MANY_LOGIN_ATTEMPTS");
        }
        catch (UserAlreadyExistsException e)
        {
            var statusCode = StatusCodes.Status409Conflict;
            _logger.LogWarning(e, "Conflict: {Message}", e.Message);
            await HandleExceptionAsync(context, statusCode, e.Message, "USER_ALREADY_EXISTS");
        }
        catch (ConflictException e)
        {
            var statusCode = StatusCodes.Status409Conflict;
            _logger.LogWarning(e, "Conflict: {Message}", e.Message);
            await HandleExceptionAsync(context, statusCode, e.Message, "CONFLICT");
        }
        catch (NotFoundException e)
        {
            var statusCode = StatusCodes.Status404NotFound;
            _logger.LogWarning(e, "Not found: {Message}", e.Message);
            await HandleExceptionAsync(context, statusCode, e.Message, "NOT_FOUND");
        }
        catch (InternalServerException e)
        {
            var statusCode = StatusCodes.Status500InternalServerError;
            _logger.LogError(e, "An unexpected error occurred.");
            await HandleExceptionAsync(context, statusCode, UnexpectedErrorMessage, "INTERNAL_SERVER_ERROR");
        }
        catch (Exception e)
        {
            var statusCode = StatusCodes.Status500InternalServerError;
            _logger.LogError(e, "An unhandled exception occurred.");
            await HandleExceptionAsync(context, statusCode, UnexpectedErrorMessage, "INTERNAL_SERVER_ERROR");
        }
    }
    
    private static async Task HandleExceptionAsync(HttpContext context, int statusCode, string message, string errorCode)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        
        var response = new ApiErrorResponse
        {
            StatusCode = statusCode,
            Message = message,
            Code = errorCode
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}
