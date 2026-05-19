using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<RegisterUserResponseDto>> Register(
        RegisterUserRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, ct);

            return StatusCode(StatusCodes.Status201Created, response);
        }

        catch (InvalidRegistrationRequestException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UserAlreadyExistsException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
