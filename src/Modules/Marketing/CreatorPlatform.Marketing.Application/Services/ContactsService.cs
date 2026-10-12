using System.Runtime.CompilerServices;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Application.Time;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class ContactsService : IContactsService
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;
    private const int GrowthMonths = 12;
    private const int ExportBatchSize = 500;
    private const string ContactNotFoundMessage = "Contact not found.";
    private const string MaxContactsLimitKey = "max_contacts";

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IContactsRepository _contactsRepository;
    private readonly IMarketingUnitOfWork _unitOfWork;
    private readonly ICreatorUsageService _usageService;

    public ContactsService(
        ICreatorContextProvider creatorContextProvider,
        IContactsRepository contactsRepository,
        IMarketingUnitOfWork unitOfWork,
        ICreatorUsageService usageService)
    {
        _creatorContextProvider = creatorContextProvider;
        _contactsRepository = contactsRepository;
        _unitOfWork = unitOfWork;
        _usageService = usageService;
    }

    public async Task<ContactsPageDto> SearchAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, string? status, string? afterEmail, int limit,
        CancellationToken ct)
    {
        var statusFilter = ParseStatus(status);
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var sourceLandingPageId = await ResolveSourceFilterAsync(context.CreatorId, landingPageId, ct);

        var clampedLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        // Fetch one extra row to detect a next page without a separate COUNT query, then trim it.
        var rows = await _contactsRepository.SearchAsync(
            context.CreatorId, search, sourceLandingPageId, statusFilter, afterEmail, clampedLimit, ct);
        var hasMore = rows.Count > clampedLimit;
        var page = hasMore ? rows.Take(clampedLimit).ToList() : rows;

        return new ContactsPageDto(page.Select(ToDto).ToList(), hasMore);
    }

    public async Task<ContactExport> StartExportAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, string? status, CancellationToken ct)
    {
        var statusFilter = ParseStatus(status);
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);
        var sourceLandingPageId = await ResolveSourceFilterAsync(context.CreatorId, landingPageId, ct);

        return new ContactExport(
            context.Slug,
            StreamContactsAsync(context.CreatorId, search, sourceLandingPageId, statusFilter, ct));
    }

    // The same keyset query as the directory page, batch after batch: each batch costs one summary scan plus one
    // titles and one unsubscribe query (no N+1), and only one batch is held in memory at a time.
    private async IAsyncEnumerable<ContactDto> StreamContactsAsync(
        int creatorId, string? search, int? sourceLandingPageId, ContactStatusFilter? status,
        [EnumeratorCancellation] CancellationToken ct)
    {
        string? afterEmail = null;

        while (true)
        {
            var rows = await _contactsRepository.SearchAsync(
                creatorId, search, sourceLandingPageId, status, afterEmail, ExportBatchSize, ct);
            var hasMore = rows.Count > ExportBatchSize;

            foreach (var row in hasMore ? rows.Take(ExportBatchSize) : rows)
            {
                afterEmail = row.Email;
                yield return ToDto(row);
            }

            if (!hasMore)
                yield break;
        }
    }

    /// <summary>"active" | "unsubscribed" (any case); empty means everyone.</summary>
    private static ContactStatusFilter? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;
        return status.Trim().ToLowerInvariant() switch
        {
            "active" => ContactStatusFilter.Active,
            "unsubscribed" => ContactStatusFilter.Unsubscribed,
            _ => throw new BadRequestException("Invalid status. Valid values: active, unsubscribed.")
        };
    }

    private async Task<int?> ResolveSourceFilterAsync(int creatorId, Guid? landingPageId, CancellationToken ct)
    {
        if (landingPageId is not { } landingPagePublicId)
            return null;

        // Same 404 whether the page doesn't exist or belongs to another creator — no cross-tenant probing.
        return await _creatorContextProvider.ResolveLandingPageIdAsync(creatorId, landingPagePublicId, ct)
            ?? throw new NotFoundException("Landing page not found.");
    }

    private static ContactDto ToDto(ContactRow row) => new(
        row.Email,
        row.FirstCapturedAt,
        row.SourcesCount,
        row.Sources,
        row.IsUnsubscribed,
        row.SourceList.Select(s => new ContactSourceDto(s.LandingPagePublicId, s.Title)).ToList());

    public async Task<ContactStatsDto> GetStatsAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        // Months are the creator's own (their timezone); the bounds are the instants those local months start.
        var local = CreatorTimeZone.LocalNow(DateTimeOffset.UtcNow, context.Timezone);
        var thisMonth = new DateTime(local.Year, local.Month, 1);
        var currentMonthStart = CreatorTimeZone.ToInstant(thisMonth, context.Timezone);
        var windowStart = CreatorTimeZone.ToInstant(thisMonth.AddMonths(-(GrowthMonths - 1)), context.Timezone);
        var windowEnd = CreatorTimeZone.ToInstant(thisMonth.AddMonths(1), context.Timezone);

        // Sequential on purpose: the queries share one scoped DbContext, which allows no concurrent use.
        var counts = await _contactsRepository.GetStatsCountsAsync(context.CreatorId, currentMonthStart, ct);
        var growth = await _contactsRepository.GetGrowthAsync(
            context.CreatorId, windowStart, currentMonthStart, windowEnd, context.Timezone, ct);
        var sources = await _contactsRepository.GetSourceCountsAsync(context.CreatorId, ct);
        var monthly = await _contactsRepository.GetMonthlyAsync(context.CreatorId, context.Timezone, ct);

        return new ContactStatsDto(
            counts.Total,
            counts.Active,
            counts.NewThisMonth,
            counts.Unsubscribed,
            growth.Select(g => new ContactGrowthPointDto(g.Month, g.Total)).ToList(),
            sources.Select(s => new ContactSourceCountDto(s.LandingPagePublicId, s.Title, s.Count)).ToList(),
            monthly.Select(m => new ContactMonthDto(m.Month, m.Joined, m.Unsubscribed)).ToList());
    }

    public async Task DeleteAsync(string slug, int ownerUserId, string email, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        // Same normalisation as capture time (EmailCaptureService) and the summary table.
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length == 0)
            throw new NotFoundException(ContactNotFoundMessage);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var deleted = await _contactsRepository.DeleteAsync(context.CreatorId, normalizedEmail, ct);

            // Nothing was deleted, so throwing here (rolling back an empty transaction) is safe.
            if (!deleted)
                throw new NotFoundException(ContactNotFoundMessage);

            // Give the contact's plan slot back in the same transaction: max_contacts counts unique contacts
            // (EmailCaptureService consumes one unit when a sign-up creates the summary row), so one deleted
            // contact refunds exactly one. AllTime counters have a single fixed period row, so asOf selects
            // nothing here.
            await _usageService.RefundAsync(
                context.CreatorId, MaxContactsLimitKey, 1, UsagePeriod.AllTime, DateTimeOffset.UtcNow, ct);
        }, ct);
    }

    private async Task<MarketingCreatorContext> GetCreatorContextAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");
    }
}
