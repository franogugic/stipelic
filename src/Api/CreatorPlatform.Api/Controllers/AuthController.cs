using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Application.Options;
using CreatorPlatform.Api.RateLimiting;
using CreatorPlatform.Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AuthOptions _authOptions;
    private readonly LoginAttemptLimiter _loginAttemptLimiter;

    public AuthController(
        IAuthService authService,
        IOptions<AuthOptions> authOptions,
        LoginAttemptLimiter loginAttemptLimiter)
    {
        _authService = authService;
        _authOptions = authOptions.Value;
        _loginAttemptLimiter = loginAttemptLimiter;
    }

    [HttpPost("register")]
    [EnableRateLimiting("Register")]
    public async Task<ActionResult<RegisterUserResponseDto>> Register(
        RegisterUserRequestDto request,
        CancellationToken ct)
    {
        var response = await _authService.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [EnableRateLimiting("Login")]
    public async Task<ActionResult<LoginUserResponseDto>> Login(
        LoginUserRequestDto request,
        CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!_loginAttemptLimiter.IsAllowed(ipAddress, request.Email))
            throw new TooManyLoginAttemptsException();

        var result = await _authService.LoginAsync(request, ct);

        Response.Cookies.Append(
            _authOptions.SessionCookieName,
            result.SessionToken,
            CreateSessionCookieOptions(result.ExpiresAt));

        return Ok(result.User);
    }

    [HttpGet("me")]
    public ActionResult<LoginUserResponseDto> Me([FromServices] ICurrentUserContext currentUserContext)
    {
        var currentUser = currentUserContext.User;
        if (currentUser is null)
        {
            return Unauthorized(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Authentication is required.",
                Code = "UNAUTHORIZED"
            });
        }

        return Ok(new LoginUserResponseDto
        {
            PublicId = currentUser.PublicId,
            FirstName = currentUser.FirstName,
            LastName = currentUser.LastName,
            Email = currentUser.Email,
            IsEmailVerified = currentUser.IsEmailVerified,
            Status = currentUser.Status
        });
    }

    [HttpPost("logout")]
    public async Task<ActionResult<LogoutResponseDto>> Logout(
        [FromServices] ICurrentUserContext currentUserContext,
        CancellationToken ct)
    {
        var currentUser = currentUserContext.User;
        if (currentUser is null)
        {
            return Unauthorized(new ApiErrorResponse
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                Message = "Authentication is required.",
                Code = "UNAUTHORIZED"
            });
        }

        var response = await _authService.LogoutAsync(currentUser.SessionId, ct);

        Response.Cookies.Delete(
            _authOptions.SessionCookieName,
            CreateSessionCookieOptions());

        return Ok(response);
    }

    [HttpPost("verify-email")]
    [EnableRateLimiting("VerifyEmail")]
    public async Task<ActionResult<VerifyEmailResponseDto>> VerifyEmail(
        VerifyEmailRequestDto request,
        CancellationToken ct)
    {
        var response = await _authService.VerifyEmailAsync(request, ct);
        return Ok(response);
    }

    [HttpPost("resend-verification-email")]
    [EnableRateLimiting("ResendVerificationEmail")]
    public async Task<ActionResult<ResendEmailVerificationResponseDto>> ResendVerificationEmail(
        ResendEmailVerificationRequestDto request,
        CancellationToken ct)
    {
        var response = await _authService.ResendEmailVerificationAsync(request, ct);
        return Ok(response);
    }

    private CookieOptions CreateSessionCookieOptions(DateTimeOffset? expires = null)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = _authOptions.SessionCookieSecure,
            SameSite = ParseSameSiteMode(_authOptions.SessionCookieSameSite),
            Expires = expires
        };

        if (!string.IsNullOrWhiteSpace(_authOptions.SessionCookieDomain))
            options.Domain = _authOptions.SessionCookieDomain;

        return options;
    }

    private static SameSiteMode ParseSameSiteMode(string sameSite)
    {
        return sameSite.Trim().ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "strict" => SameSiteMode.Strict,
            "lax" => SameSiteMode.Lax,
            _ => SameSiteMode.Lax
        };
    }
}
