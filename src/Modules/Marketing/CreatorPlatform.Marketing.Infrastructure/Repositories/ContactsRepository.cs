using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

/// <summary>Reads exclusively from the materialized <c>marketing.contact_summaries</c> table — never
/// re-aggregates <c>analytics.email_captures</c> on a page load. Source landing pages and the unsubscribed
/// flag are resolved in two extra queries bounded to the page being returned (≤ limit + 1 rows), not
/// per-row correlated subqueries.</summary>
public sealed class ContactsRepository : IContactsRepository
{
    private sealed record SummaryRow(string Email, DateTimeOffset FirstCapturedAt, List<int> SourceLandingPageIds);

    private readonly CreatorPlatformDbContext _context;

    public ContactsRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContactRow>> SearchAsync(
        int creatorId, string? search, int? landingPageId, string? afterEmail, int limit, CancellationToken ct)
    {
        var searchPrefix = string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();
        var after = afterEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        var fetchLimit = limit + 1;

        // Plain keyset scan on the (CreatorId, Email) unique index — no aggregation over capture history.
        // The source filter is written as containment (@>), not `= ANY(...)`: only @> can use the GIN index on
        // SourceLandingPageIds, which is what keeps a rare source fast (the btree walk would otherwise scan the
        // creator's whole directory to fill one page). A null filter folds away at plan time.
        var summaries = await _context.Database.SqlQuery<SummaryRow>($"""
            SELECT
                "Email" AS "Email",
                "FirstCapturedAt" AS "FirstCapturedAt",
                "SourceLandingPageIds" AS "SourceLandingPageIds"
            FROM marketing.contact_summaries
            WHERE "CreatorId" = {creatorId}
              AND "Email" > {after}
              AND ({searchPrefix}::text IS NULL OR "Email" LIKE {searchPrefix}::text || '%')
              AND ({landingPageId}::int IS NULL OR "SourceLandingPageIds" @> ARRAY[{landingPageId}::int])
            ORDER BY "Email"
            LIMIT {fetchLimit}
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        if (summaries.Count == 0)
            return [];

        // Resolve source landing pages only for ids that actually appear on this page (bounded by fetchLimit
        // rows, not the creator's whole history) — one query, not one per contact. No status filter: archived
        // pages still label their contacts.
        var landingPageIds = summaries.SelectMany(s => s.SourceLandingPageIds).Distinct().ToList();
        var sourcesById = await _context.Set<LandingPage>()
            .AsNoTracking()
            .Where(lp => lp.CreatorId == creatorId && landingPageIds.Contains(lp.Id))
            .Select(lp => new { lp.Id, lp.PublicId, lp.Title })
            .ToDictionaryAsync(lp => lp.Id, lp => new ContactSourceRow(lp.PublicId, lp.Title), ct);

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
                // SourceLandingPageIds is appended to on each new page's first capture, so array order is
                // first-capture order.
                var sourceList = s.SourceLandingPageIds
                    .Select(id => sourcesById.GetValueOrDefault(id))
                    .OfType<ContactSourceRow>()
                    .ToList();

                var titles = sourceList
                    .Select(source => source.Title)
                    .OrderBy(title => title, StringComparer.Ordinal)
                    .Take(3);

                return new ContactRow(
                    s.Email,
                    s.FirstCapturedAt,
                    s.SourceLandingPageIds.Count,
                    string.Join(", ", titles),
                    unsubscribedEmails.Contains(s.Email),
                    sourceList);
            })
            .ToList();
    }

    public async Task<ContactStatsCountsRow> GetStatsCountsAsync(int creatorId, DateTimeOffset monthStart, CancellationToken ct)
    {
        // One pass over the creator's summaries. Active is an anti-join written as LEFT JOIN … IS NULL so the
        // planner can hash the (small) unsubscribe set once; a NOT EXISTS inside the aggregate FILTER runs as
        // a per-row SubPlan instead (~150 ms vs ~14 ms at 100k contacts). No row multiplication: unsubscribes
        // is unique on (CreatorId, Email). An aggregate without GROUP BY always yields exactly one row.
        var rows = await _context.Database.SqlQuery<ContactStatsCountsRow>($"""
            SELECT
                COUNT(*)::int AS "Total",
                (COUNT(*) FILTER (WHERE u."Id" IS NULL))::int AS "Active",
                (COUNT(*) FILTER (WHERE cs."FirstCapturedAt" >= {monthStart}))::int AS "NewThisMonth",
                (SELECT COUNT(*) FROM marketing.unsubscribes u2 WHERE u2."CreatorId" = {creatorId})::int AS "Unsubscribed"
            FROM marketing.contact_summaries cs
            LEFT JOIN marketing.unsubscribes u
                ON u."CreatorId" = {creatorId}
               AND u."Email" = cs."Email"
            WHERE cs."CreatorId" = {creatorId}
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Single();
    }

    public async Task<List<ContactGrowthRow>> GetGrowthAsync(
        int creatorId, DateTimeOffset windowStart, DateTimeOffset lastMonthStart, CancellationToken ct)
    {
        var windowEnd = lastMonthStart.AddMonths(1);

        // Month arithmetic runs on UTC `timestamp` values (AT TIME ZONE 'UTC'), never on timestamptz, so the
        // buckets don't depend on the session TimeZone (adding '1 month' to a timestamptz is DST-sensitive).
        // Contacts from before the window are clamped into the first bucket by GREATEST, so the running
        // SUM() OVER starts from the creator's whole base instead of zero — one query, no second COUNT.
        return await _context.Database.SqlQuery<ContactGrowthRow>($"""
            SELECT
                to_char(m."MonthStart", 'YYYY-MM') AS "Month",
                (SUM(COALESCE(b."Count", 0)) OVER (ORDER BY m."MonthStart"))::int AS "Total"
            FROM generate_series(
                {windowStart}::timestamptz AT TIME ZONE 'UTC',
                {lastMonthStart}::timestamptz AT TIME ZONE 'UTC',
                interval '1 month') AS m("MonthStart")
            LEFT JOIN (
                SELECT
                    GREATEST(
                        date_trunc('month', cs."FirstCapturedAt" AT TIME ZONE 'UTC'),
                        {windowStart}::timestamptz AT TIME ZONE 'UTC') AS "Bucket",
                    COUNT(*) AS "Count"
                FROM marketing.contact_summaries cs
                WHERE cs."CreatorId" = {creatorId}
                  AND cs."FirstCapturedAt" < {windowEnd}
                GROUP BY 1
            ) b ON b."Bucket" = m."MonthStart"
            ORDER BY m."MonthStart"
            """)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<ContactDeletionRow?> DeleteAsync(int creatorId, string email, CancellationToken ct)
    {
        // Summary first: its RETURNING doubles as the existence check, so an unknown contact deletes nothing.
        var deletedSummaryIds = await _context.Database.SqlQuery<int>($"""
            DELETE FROM marketing.contact_summaries
            WHERE "CreatorId" = {creatorId} AND "Email" = {email}
            RETURNING "Id" AS "Value"
            """)
            .ToListAsync(ct);

        if (deletedSummaryIds.Count == 0)
            return null;

        // Only captures on this creator's landing pages — the same address captured by another creator stays
        // theirs. Probes the (LandingPageId, Email) unique index once per page of the creator.
        // marketing.unsubscribes (an opt-out must outlive the contact) and marketing.campaign_recipients (send
        // history) are deliberately left alone.
        var captureLandingPageIds = await _context.Database.SqlQuery<int>($"""
            DELETE FROM analytics.email_captures ec
            USING landing_pages.landing_pages lp
            WHERE lp."Id" = ec."LandingPageId"
              AND lp."CreatorId" = {creatorId}
              AND ec."Email" = {email}
            RETURNING ec."LandingPageId" AS "Value"
            """)
            .ToListAsync(ct);

        return new ContactDeletionRow(captureLandingPageIds);
    }

    public async Task<List<ContactSourceCountRow>> GetSourceCountsAsync(int creatorId, CancellationToken ct)
    {
        // Aggregate on the internal id first, then join only the distinct pages for their public id/title.
        // No status filter: archived pages still own their captured contacts. The CreatorId condition on the
        // join is defence in depth — SourceLandingPageIds only ever holds this creator's pages.
        return await _context.Database.SqlQuery<ContactSourceCountRow>($"""
            SELECT
                lp."PublicId" AS "LandingPagePublicId",
                lp."Title" AS "Title",
                s."Count" AS "Count"
            FROM (
                SELECT src."LandingPageId", COUNT(*)::int AS "Count"
                FROM marketing.contact_summaries cs
                CROSS JOIN LATERAL unnest(cs."SourceLandingPageIds") AS src("LandingPageId")
                WHERE cs."CreatorId" = {creatorId}
                GROUP BY src."LandingPageId"
            ) s
            JOIN landing_pages.landing_pages lp
                ON lp."Id" = s."LandingPageId"
               AND lp."CreatorId" = {creatorId}
            ORDER BY s."Count" DESC, lp."Title", lp."Id"
            """)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
