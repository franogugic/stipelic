using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/contacts")]
public sealed class ContactsController : ControllerBase
{
    private readonly IContactsService _contactsService;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IHomeSummaryCache _homeSummaryCache;
    private readonly ILandingPageTimeSeriesCache _timeSeriesCache;

    public ContactsController(
        IContactsService contactsService,
        ICurrentUserContext currentUserContext,
        IHomeSummaryCache homeSummaryCache,
        ILandingPageTimeSeriesCache timeSeriesCache)
    {
        _contactsService = contactsService;
        _currentUserContext = currentUserContext;
        _homeSummaryCache = homeSummaryCache;
        _timeSeriesCache = timeSeriesCache;
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

    /// <summary>CSV download of the directory (same <paramref name="search"/> / <paramref name="landingPageId"/>
    /// filters as the list), streamed batch by batch.</summary>
    [HttpGet("export")]
    [EnableRateLimiting("ExportContacts")]
    public async Task Export(
        string slug,
        [FromQuery] string? search,
        [FromQuery] Guid? landingPageId,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        // Ownership and the page filter are checked here, so a 401/403/404 still answers as JSON before any CSV.
        var export = await _contactsService.StartExportAsync(slug, currentUser.Id, search, landingPageId, ct);

        await CsvResponseWriter.WriteAsync(
            Response,
            ContactsCsv.FileName(export.CreatorSlug, DateTimeOffset.UtcNow),
            ContactsCsv.Header,
            export.Contacts.Select(ContactsCsv.Row),
            ct);
    }

    /// <summary>Removes a contact (summary + captures on this creator's pages). The email is the URL-encoded
    /// path segment, normalised server-side. Opt-outs and send history are kept.</summary>
    [HttpDelete("{email}")]
    [EnableRateLimiting("DeleteContact")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string slug, string email, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var result = await _contactsService.DeleteAsync(slug, currentUser.Id, email, ct);

        // Every cache that counts captures or contacts: the home summary's SubscriberCount, and each affected
        // landing page's analytics time series (CaptureCount per bucket). The views summary holds only views and
        // unique visitors, so it is unaffected.
        _homeSummaryCache.Remove(slug);
        foreach (var landingPageId in result.AffectedLandingPageIds)
            _timeSeriesCache.Remove(landingPageId);

        return Ok(ApiResponse<object>.Success(StatusCodes.Status200OK, "Contact deleted.", null));
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
