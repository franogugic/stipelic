using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Repositories;

public sealed class ContactsRepository : IContactsRepository
{
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

        // Single query: dedupes captures per email across every landing page the creator owns, counts
        // distinct sources, and names up to 3 of them via a correlated subquery with its own LIMIT
        // (string_agg over a bounded inner SELECT, not the full source list). Keyset pagination on
        // Email (already normalized lowercase at capture time) keeps page 2+ a plain indexable range
        // scan instead of an OFFSET.
        return await _context.Database.SqlQuery<ContactRow>($"""
            SELECT
                ec."Email" AS "Email",
                MIN(ec."CapturedAt") AS "FirstCapturedAt",
                COUNT(DISTINCT ec."LandingPageId")::int AS "SourcesCount",
                COALESCE((
                    SELECT string_agg(t."Title", ', ')
                    FROM (
                        SELECT DISTINCT lp2."Title"
                        FROM analytics.email_captures ec2
                        JOIN landing_pages.landing_pages lp2 ON lp2."Id" = ec2."LandingPageId"
                        WHERE ec2."Email" = ec."Email" AND lp2."CreatorId" = {creatorId}
                        ORDER BY lp2."Title"
                        LIMIT 3
                    ) t
                ), '') AS "Sources",
                EXISTS (
                    SELECT 1 FROM marketing.unsubscribes u
                    WHERE u."CreatorId" = {creatorId} AND u."Email" = ec."Email"
                ) AS "IsUnsubscribed"
            FROM analytics.email_captures ec
            JOIN landing_pages.landing_pages lp ON lp."Id" = ec."LandingPageId"
            WHERE lp."CreatorId" = {creatorId}
              AND ec."Email" > {after}
              AND ({searchPrefix}::text IS NULL OR ec."Email" LIKE {searchPrefix}::text || '%')
            GROUP BY ec."Email"
            ORDER BY ec."Email"
            LIMIT {fetchLimit}
            """)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
