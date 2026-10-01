using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Marketing.Application.Services;

public sealed class ContactsService : IContactsService
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;
    private const int GrowthMonths = 12;

    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly IContactsRepository _contactsRepository;

    public ContactsService(ICreatorContextProvider creatorContextProvider, IContactsRepository contactsRepository)
    {
        _creatorContextProvider = creatorContextProvider;
        _contactsRepository = contactsRepository;
    }

    public async Task<ContactsPageDto> SearchAsync(
        string slug, int ownerUserId, string? search, Guid? landingPageId, string? afterEmail, int limit,
        CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        int? sourceLandingPageId = null;
        if (landingPageId is { } landingPagePublicId)
        {
            // Same 404 whether the page doesn't exist or belongs to another creator — no cross-tenant probing.
            sourceLandingPageId = await _creatorContextProvider.ResolveLandingPageIdAsync(
                    context.CreatorId, landingPagePublicId, ct)
                ?? throw new NotFoundException("Landing page not found.");
        }

        var clampedLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);

        // Fetch one extra row to detect a next page without a separate COUNT query, then trim it.
        var rows = await _contactsRepository.SearchAsync(
            context.CreatorId, search, sourceLandingPageId, afterEmail, clampedLimit, ct);
        var hasMore = rows.Count > clampedLimit;
        var page = hasMore ? rows.Take(clampedLimit).ToList() : rows;

        var contacts = page
            .Select(r => new ContactDto(
                r.Email,
                r.FirstCapturedAt,
                r.SourcesCount,
                r.Sources,
                r.IsUnsubscribed,
                r.SourceList.Select(s => new ContactSourceDto(s.LandingPagePublicId, s.Title)).ToList()))
            .ToList();

        return new ContactsPageDto(contacts, hasMore);
    }

    public async Task<ContactStatsDto> GetStatsAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var now = DateTimeOffset.UtcNow;
        var currentMonthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var windowStart = currentMonthStart.AddMonths(-(GrowthMonths - 1));

        // Sequential on purpose: the queries share one scoped DbContext, which allows no concurrent use.
        var counts = await _contactsRepository.GetStatsCountsAsync(context.CreatorId, currentMonthStart, ct);
        var growth = await _contactsRepository.GetGrowthAsync(context.CreatorId, windowStart, currentMonthStart, ct);
        var sources = await _contactsRepository.GetSourceCountsAsync(context.CreatorId, ct);

        return new ContactStatsDto(
            counts.Total,
            counts.Active,
            counts.NewThisMonth,
            counts.Unsubscribed,
            growth.Select(g => new ContactGrowthPointDto(g.Month, g.Total)).ToList(),
            sources.Select(s => new ContactSourceCountDto(s.LandingPagePublicId, s.Title, s.Count)).ToList());
    }

    private async Task<MarketingCreatorContext> GetCreatorContextAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");
    }
}
