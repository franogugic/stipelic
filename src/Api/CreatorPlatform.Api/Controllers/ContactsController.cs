using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/contacts")]
public sealed class ContactsController : ControllerBase
{
    private readonly IContactsService _contactsService;
    private readonly ICurrentUserContext _currentUserContext;

    public ContactsController(IContactsService contactsService, ICurrentUserContext currentUserContext)
    {
        _contactsService = contactsService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ContactsPageDto>>> Search(
        string slug,
        [FromQuery] string? search,
        [FromQuery] Guid? landingPageId,
        [FromQuery] string? afterEmail,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var page = await _contactsService.SearchAsync(
            slug, currentUser.Id, search, landingPageId, afterEmail, limit, ct);

        return Ok(ApiResponse<ContactsPageDto>.Success(
            StatusCodes.Status200OK,
            "Contacts loaded.",
            page));
    }

    /// <summary>Headline counts, 12-month cumulative growth and per-landing-page counts for the
    /// subscribers overview.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<ContactStatsDto>>> GetStats(string slug, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var stats = await _contactsService.GetStatsAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<ContactStatsDto>.Success(
            StatusCodes.Status200OK,
            "Contact stats loaded.",
            stats));
    }

    /// <summary>Returns the authenticated user or throws 401.</summary>
    private CurrentUserDto GetAuthenticatedUser()
    {
        var user = _currentUserContext.User;
        if (user is null)
            throw new UnauthorizedException("Authentication is required.");
        return user;
    }

    /// <summary>Returns the authenticated and email-verified user or throws 401/403.</summary>
    private CurrentUserDto GetVerifiedUser()
    {
        var user = GetAuthenticatedUser();
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        return user;
    }
}
