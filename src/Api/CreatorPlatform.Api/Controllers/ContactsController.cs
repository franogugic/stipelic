using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Api.RateLimiting;
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
    private readonly IOrderService _orderService;
    private readonly IContactImportService _contactImportService;

    /// <summary>The largest CSV an import accepts.</summary>
    public const long MaxImportFileBytes = 5 * 1024 * 1024;

    // The whole multipart request: the file plus the two small fields and the multipart framing. A file just over
    // the limit still reaches the action and gets a 400 FILE_TOO_LARGE; a far bigger request is refused earlier.
    private const long MaxImportRequestBytes = MaxImportFileBytes + 64 * 1024;

    public ContactsController(
        IContactsService contactsService,
        ICurrentUserContext currentUserContext,
        IOrderService orderService,
        IContactImportService contactImportService)
    {
        _contactsService = contactsService;
        _currentUserContext = currentUserContext;
        _orderService = orderService;
        _contactImportService = contactImportService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<ContactsPageDto>>> Search(
        string slug,
        [FromQuery] string? search,
        [FromQuery] Guid? landingPageId,
        [FromQuery] string? status,
        [FromQuery] string? afterEmail,
        [FromQuery] int limit,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var page = await _contactsService.SearchAsync(
            slug, currentUser.Id, search, landingPageId, status, afterEmail, limit, ct);

        return Ok(ApiResponse<ContactsPageDto>.Success(
            StatusCodes.Status200OK,
            "Contacts loaded.",
            page));
    }

    /// <summary>Headline counts, 12-month cumulative growth and per-landing-page counts for the subscribers overview.
    /// With <paramref name="period"/> (today | 7d | 15d | 30d | 2m | 3m | 6m | all) the growth and the joined / unsubscribed
    /// figures come as that period's buckets instead (<see cref="ContactPeriodStatsDto"/>).</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(string slug, [FromQuery] string? period, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        if (!string.IsNullOrWhiteSpace(period))
        {
            var periodStats = await _contactsService.GetPeriodStatsAsync(slug, currentUser.Id, period, ct);

            return Ok(ApiResponse<ContactPeriodStatsDto>.Success(
                StatusCodes.Status200OK,
                "Contact stats loaded.",
                periodStats));
        }

        var stats = await _contactsService.GetStatsAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<ContactStatsDto>.Success(
            StatusCodes.Status200OK,
            "Contact stats loaded.",
            stats));
    }

    /// <summary>CSV download of the directory (same <paramref name="search"/> / <paramref name="landingPageId"/> /
    /// <paramref name="status"/> filters as the list), streamed batch by batch.</summary>
    [HttpGet("export")]
    [EnableRateLimiting(RateLimitPolicies.ExportContacts)]
    public async Task Export(
        string slug,
        [FromQuery] string? search,
        [FromQuery] Guid? landingPageId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        // Ownership and the page filter are checked here, so a 401/403/404 still answers as JSON before any CSV.
        var export = await _contactsService.StartExportAsync(slug, currentUser.Id, search, landingPageId, status, ct);

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
    [EnableRateLimiting(RateLimitPolicies.DeleteContact)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string slug, string email, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        await _contactsService.DeleteAsync(slug, currentUser.Id, email, ct);

        // The one cache that counts contacts is the home summary's SubscriberCount. The views summary holds only
        // views and unique visitors, so it is unaffected, and the analytics series are not cached.
        await _orderService.InvalidateHomeSummaryAsync(slug, currentUser.Id);

        return Ok(ApiResponse<object>.Success(StatusCodes.Status200OK, "Contact deleted.", null));
    }

    /// <summary>
    /// Imports contacts from a CSV file (multipart: <c>file</c>, <c>consentAttested</c>, <c>dryRun</c>). With
    /// <c>dryRun=true</c> nothing is written and the summary shows what a real import would do; the client then sends
    /// the same file with <c>dryRun=false</c> and the consent attestation (stateless: no upload is stored). A real import
    /// is all or nothing against the contact limit (409 CONTACT_LIMIT_EXCEEDED). File problems are 400s with a code
    /// (<see cref="ContactImportErrors"/>); invalid rows are reported in the summary, not as errors.
    /// </summary>
    [HttpPost("import")]
    [EnableRateLimiting(RateLimitPolicies.ImportContacts)]
    [RequestSizeLimit(MaxImportRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportRequestBytes)]
    public async Task<ActionResult<ApiResponse<ContactImportSummaryDto>>> Import(
        string slug,
        IFormFile? file,
        [FromForm] bool consentAttested,
        [FromForm] bool dryRun,
        CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        if (file is null)
            throw new BadRequestException("Choose a CSV file to import.", ContactImportErrors.FileMissing);
        if (file.Length == 0)
            throw new BadRequestException("The file is empty.", ContactImportErrors.FileEmpty);
        if (file.Length > MaxImportFileBytes)
            throw new BadRequestException("The file is larger than 5 MB.", ContactImportErrors.FileTooLarge);

        await using var stream = file.OpenReadStream();
        var summary = await _contactImportService.ImportAsync(
            slug, currentUser.Id, file.FileName, stream, consentAttested, dryRun, ct);

        // The home summary caches the subscriber count.
        if (!dryRun && summary.New > 0)
            await _orderService.InvalidateHomeSummaryAsync(slug, currentUser.Id);

        return Ok(ApiResponse<ContactImportSummaryDto>.Success(
            StatusCodes.Status200OK,
            dryRun ? "Import checked." : $"{summary.New} contact(s) imported.",
            summary));
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
