using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators")]
public sealed class CreatorsController : ControllerBase
{
    private readonly ICreatorService _creatorService;
    private readonly ICurrentUserContext _currentUserContext;

    public CreatorsController(
        ICreatorService creatorService,
        ICurrentUserContext currentUserContext)
    {
        _creatorService = creatorService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet("me")]
    public async Task<ActionResult<CreatorResponseDto?>> Me(CancellationToken ct)
    {
        var currentUser = _currentUserContext.User;
        if (currentUser is null)
            throw new UnauthorizedException("Authentication is required.");

        var response = await _creatorService.GetCurrentForOwnerAsync(currentUser.Id, ct);

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<CreatorResponseDto>> Create(
        [FromBody] CreateCreatorRequestDto request,
        CancellationToken ct)
    {
        var currentUser = _currentUserContext.User;
        if (currentUser is null)
            throw new UnauthorizedException("Authentication is required.");

        if (!currentUser.IsEmailVerified)
            throw new EmailNotVerifiedException();

        var response = await _creatorService.CreateAsync(currentUser.Id, request, ct);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe(CancellationToken ct)
    {
        var currentUser = _currentUserContext.User;
        if (currentUser is null)
            throw new UnauthorizedException("Authentication is required.");

        if (!currentUser.IsEmailVerified)
            throw new EmailNotVerifiedException();
        await _creatorService.DeleteCurrentAsync(currentUser.Id, ct);

        return NoContent();
    }
}
