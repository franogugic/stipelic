using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
