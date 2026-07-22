using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

/// <summary>Reads exclusively from the materialized <c>marketing.contact_summaries</c> table — never
/// re-aggregates <c>analytics.email_captures</c> on a page load. Source landing page names and the
/// unsubscribed flag are resolved in two extra queries bounded to the page being returned (≤ limit + 1
/// rows), not per-row correlated subqueries.</summary>
public sealed class ContactsRepository : IContactsRepository
{
    private sealed record SummaryRow(string Email, DateTimeOffset FirstCapturedAt, List<int> SourceLandingPageIds);

    private readonly CreatorPlatformDbContext _context;

    public ContactsRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContactRow>> SearchAsync(int creatorId, string? search, string? afterEmail, int limit, CancellationToken ct)
    {
        var searchPrefix = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
        var after = afterEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        var fetchLimit = limit + 1;

        // Plain keyset scan on the (CreatorId, Email) unique index — no aggregation over capture history.
        var summaries = await _context.Database.SqlQuery<SummaryRow>($"""
            SELECT
                "Email" AS "Email",
                "FirstCapturedAt" AS "FirstCapturedAt",
                "SourceLandingPageIds" AS "SourceLandingPageIds"
            FROM marketing.contact_summaries
            WHERE "CreatorId" = {creatorId}
              AND "Email" > {after}
              AND ({searchPrefix}::text IS NULL OR "Email" LIKE {searchPrefix}::text || '%')
            ORDER BY "Email"
            LIMIT {fetchLimit}
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        if (summaries.Count == 0)
            return [];

        // Resolve source landing page titles only for ids that actually appear on this page (bounded by
        // fetchLimit rows, not the creator's whole history) — one query, not one per contact.
        var landingPageIds = summaries.SelectMany(s => s.SourceLandingPageIds).Distinct().ToList();
        var titlesById = await _context.Set<LandingPage>()
            .AsNoTracking()
            .Where(lp => landingPageIds.Contains(lp.Id))
            .Select(lp => new { lp.Id, lp.Title })
            .ToDictionaryAsync(lp => lp.Id, lp => lp.Title, ct);

        // Same bound: unsubscribed status resolved for just the emails on this page, one query.
        var emails = summaries.Select(s => s.Email).ToList();
        var unsubscribedEmails = (await _context.Set<Unsubscribe>()
            .AsNoTracking()
            .Where(u => u.CreatorId == creatorId && emails.Contains(u.Email))
            .Select(u => u.Email)
            .ToListAsync(ct))
            .ToHashSet();

        return summaries
            .Select(s =>
            {
                var titles = s.SourceLandingPageIds
                    .Select(id => titlesById.GetValueOrDefault(id))
                    .Where(title => title is not null)
                    .OrderBy(title => title, StringComparer.Ordinal)
                    .Take(3);

                return new ContactRow(
                    s.Email,
                    s.FirstCapturedAt,
                    s.SourceLandingPageIds.Count,
                    string.Join(", ", titles),
                    unsubscribedEmails.Contains(s.Email));
            })
            .ToList();
    }
}
