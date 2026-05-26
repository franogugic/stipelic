using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Application.Options;
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

    public AuthController(IAuthService authService, IOptions<AuthOptions> authOptions)
    {
        _authService = authService;
        _authOptions = authOptions.Value;
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
        var result = await _authService.LoginAsync(request, ct);

        Response.Cookies.Append(
            _authOptions.SessionCookieName,
            result.SessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = result.ExpiresAt
            });

        return Ok(result.User);
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
}
