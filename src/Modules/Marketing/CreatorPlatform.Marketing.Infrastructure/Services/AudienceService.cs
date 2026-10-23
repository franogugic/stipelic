using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Domain.Contacts;
using CreatorPlatform.Marketing.Domain.Unsubscribes;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Marketing.Infrastructure.Services;

public sealed class AudienceService : IAudienceService
{
    private readonly CreatorPlatformDbContext _context;

    public AudienceService(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetAudienceCountAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int? sourceCampaignId, int creatorId, CancellationToken ct)
    {
        if (audienceType == CampaignAudienceType.LandingPageBuyers)
        {
            return (await _context.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" FROM (
                    SELECT DISTINCT lower(btrim(o."Email")) AS "Email"
                    FROM orders.orders o
                    WHERE o."LandingPageId" = {landingPageId} AND o."CreatorId" = {creatorId}
                      AND o."Status" = 'Paid' AND o."Email" IS NOT NULL AND btrim(o."Email") <> ''
                ) b
                WHERE NOT EXISTS (
                    SELECT 1 FROM marketing.unsubscribes u
                    WHERE u."CreatorId" = {creatorId} AND u."Email" = b."Email"
                )
                """).ToListAsync(ct)).Single();
        }
        if (audienceType == CampaignAudienceType.CampaignNonOpeners)
            return await NonOpeners(RequireSource(sourceCampaignId), creatorId).CountAsync(ct);

        return await BuildAudienceQuery(audienceType, landingPageId, productId, creatorId).CountAsync(ct);
    }

    public async Task<List<string>> GetAudienceEmailsAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int? sourceCampaignId, int creatorId, CancellationToken ct)
    {
        if (audienceType == CampaignAudienceType.LandingPageBuyers)
        {
            return (await _context.Database.SqlQuery<EmailRow>($"""
                SELECT b."Email" AS "Email" FROM (
                    SELECT DISTINCT lower(btrim(o."Email")) AS "Email"
                    FROM orders.orders o
                    WHERE o."LandingPageId" = {landingPageId} AND o."CreatorId" = {creatorId}
                      AND o."Status" = 'Paid' AND o."Email" IS NOT NULL AND btrim(o."Email") <> ''
                ) b
                WHERE NOT EXISTS (
                    SELECT 1 FROM marketing.unsubscribes u
                    WHERE u."CreatorId" = {creatorId} AND u."Email" = b."Email"
                )
                ORDER BY b."Email"
                """).AsNoTracking().ToListAsync(ct)).Select(r => r.Email).ToList();
        }
        if (audienceType == CampaignAudienceType.CampaignNonOpeners)
            return await NonOpeners(RequireSource(sourceCampaignId), creatorId).Select(r => r.Email).ToListAsync(ct);

        return await BuildAudienceQuery(audienceType, landingPageId, productId, creatorId).ToListAsync(ct);
    }

    public async Task<(List<string> Emails, bool HasMore)> GetAudiencePageAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int? sourceCampaignId, int creatorId,
        string? afterEmail, int limit, CancellationToken ct)
    {
        var after = afterEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        var fetchLimit = limit + 1;

        if (audienceType == CampaignAudienceType.CampaignNonOpeners)
        {
            var page = await NonOpeners(RequireSource(sourceCampaignId), creatorId)
                .Where(r => string.Compare(r.Email, after) > 0)
                .OrderBy(r => r.Email)
                .Take(fetchLimit)
                .Select(r => r.Email)
                .ToListAsync(ct);

            return page.Count > limit ? (page.Take(limit).ToList(), true) : (page, false);
        }

        // Raw SQL keyset scan (same pattern as ContactsRepository.SearchAsync) — Npgsql's EF provider does
        // not translate string comparison after a LINQ Distinct/Union, so this can't be a plain IQueryable
        // WHERE+OrderBy tacked onto BuildAudienceQuery. The All audience needs no DISTINCT: contact_summaries
        // is already unique per (CreatorId, Email).
        var rows = audienceType == CampaignAudienceType.All
            ? await _context.Database.SqlQuery<EmailRow>($"""
                SELECT cs."Email" AS "Email"
                FROM marketing.contact_summaries cs
                WHERE cs."CreatorId" = {creatorId}
                  AND cs."Email" > {after}
                  AND NOT EXISTS (
                      SELECT 1 FROM marketing.unsubscribes u
                      WHERE u."CreatorId" = {creatorId} AND u."Email" = cs."Email"
                  )
                ORDER BY cs."Email"
                LIMIT {fetchLimit}
                """)
                .AsNoTracking()
                .ToListAsync(ct)
            : audienceType == CampaignAudienceType.LandingPageBuyers
            ? await _context.Database.SqlQuery<EmailRow>($"""
                SELECT b."Email" AS "Email" FROM (
                    SELECT DISTINCT lower(btrim(o."Email")) AS "Email"
                    FROM orders.orders o
                    WHERE o."LandingPageId" = {landingPageId} AND o."CreatorId" = {creatorId}
                      AND o."Status" = 'Paid' AND o."Email" IS NOT NULL AND btrim(o."Email") <> ''
                ) b
                WHERE b."Email" > {after}
                  AND NOT EXISTS (
                      SELECT 1 FROM marketing.unsubscribes u
                      WHERE u."CreatorId" = {creatorId} AND u."Email" = b."Email"
                  )
                ORDER BY b."Email"
                LIMIT {fetchLimit}
                """)
                .AsNoTracking()
                .ToListAsync(ct)
            : audienceType == CampaignAudienceType.LandingPage
            ? await _context.Database.SqlQuery<EmailRow>($"""
                SELECT DISTINCT ec."Email" AS "Email"
                FROM analytics.email_captures ec
                WHERE ec."LandingPageId" = {landingPageId}
                  AND ec."Email" > {after}
                  AND NOT EXISTS (
                      SELECT 1 FROM marketing.unsubscribes u
                      WHERE u."CreatorId" = {creatorId} AND u."Email" = ec."Email"
                  )
                ORDER BY ec."Email"
                LIMIT {fetchLimit}
                """)
                .AsNoTracking()
                .ToListAsync(ct)
            : await _context.Database.SqlQuery<EmailRow>($"""
                SELECT "Email" FROM (
                    SELECT ec."Email" AS "Email"
                    FROM analytics.email_captures ec
                    WHERE ec."ProductId" = {productId}
                    UNION
                    SELECT ec."Email" AS "Email"
                    FROM analytics.email_captures ec
                    JOIN landing_pages.landing_pages lp ON lp."Id" = ec."LandingPageId"
                    WHERE lp."ProductId" = {productId}
                ) AS combined
                WHERE "Email" > {after}
                  AND NOT EXISTS (
                      SELECT 1 FROM marketing.unsubscribes u
                      WHERE u."CreatorId" = {creatorId} AND u."Email" = combined."Email"
                  )
                ORDER BY "Email"
                LIMIT {fetchLimit}
                """)
                .AsNoTracking()
                .ToListAsync(ct);

        var hasMore = rows.Count > limit;
        var emails = (hasMore ? rows.Take(limit) : rows).Select(r => r.Email).ToList();
        return (emails, hasMore);
    }

    public async Task<CampaignAudiencesDto> GetAudienceOverviewAsync(int creatorId, CancellationToken ct)
    {
        var allCount = await BuildAudienceQuery(CampaignAudienceType.All, null, null, creatorId).CountAsync(ct);

        // LEFT JOINs keep targets with no captures yet (count 0). The unsubscribe filter sits in the JOIN
        // condition (not WHERE) for the same reason, and COUNT(DISTINCT) dedupes an email captured twice.
        var landingPages = await _context.Database.SqlQuery<LandingPageAudienceRow>($"""
            SELECT lp."PublicId" AS "PublicId", lp."Title" AS "Title", COUNT(DISTINCT ec."Email")::int AS "RecipientCount"
            FROM landing_pages.landing_pages lp
            LEFT JOIN analytics.email_captures ec
                ON ec."LandingPageId" = lp."Id"
               AND NOT EXISTS (
                   SELECT 1 FROM marketing.unsubscribes u
                   WHERE u."CreatorId" = {creatorId} AND u."Email" = ec."Email"
               )
            WHERE lp."CreatorId" = {creatorId} AND lp."Status" = 'Published'
            GROUP BY lp."Id", lp."PublicId", lp."Title"
            ORDER BY "RecipientCount" DESC, lp."Title", lp."Id"
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        // Product audience mirrors BuildAudienceQuery: captures tagged with the product directly UNION
        // captures on any of the creator's landing pages that point at it (the UNION dedupes both sides).
        var products = await _context.Database.SqlQuery<ProductAudienceRow>($"""
            SELECT p."PublicId" AS "PublicId", p."Name" AS "Name", COUNT(DISTINCT x."Email")::int AS "RecipientCount"
            FROM products.products p
            LEFT JOIN (
                SELECT ec."ProductId" AS "ProductId", ec."Email" AS "Email"
                FROM analytics.email_captures ec
                WHERE ec."ProductId" IN (
                    SELECT p2."Id" FROM products.products p2
                    WHERE p2."CreatorId" = {creatorId} AND p2."Status" = 'Active'
                )
                UNION
                SELECT lp."ProductId" AS "ProductId", ec."Email" AS "Email"
                FROM landing_pages.landing_pages lp
                JOIN analytics.email_captures ec ON ec."LandingPageId" = lp."Id"
                WHERE lp."CreatorId" = {creatorId} AND lp."ProductId" IS NOT NULL
            ) x
                ON x."ProductId" = p."Id"
               AND NOT EXISTS (
                   SELECT 1 FROM marketing.unsubscribes u
                   WHERE u."CreatorId" = {creatorId} AND u."Email" = x."Email"
               )
            WHERE p."CreatorId" = {creatorId} AND p."Status" = 'Active'
            GROUP BY p."Id", p."PublicId", p."Name"
            ORDER BY "RecipientCount" DESC, p."Name", p."Id"
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        // Buyers: the creator's Sales pages with their paid-order buyers (same normalisation and suppression as
        // GetAudienceCountAsync). Archived pages drop out unless they still have buyers. One query for every page.
        var buyers = await _context.Database.SqlQuery<LandingPageAudienceRow>($"""
            SELECT lp."PublicId" AS "PublicId", lp."Title" AS "Title", COUNT(DISTINCT b."Email")::int AS "RecipientCount"
            FROM landing_pages.landing_pages lp
            LEFT JOIN (
                SELECT o."LandingPageId" AS "LandingPageId", lower(btrim(o."Email")) AS "Email"
                FROM orders.orders o
                WHERE o."CreatorId" = {creatorId} AND o."Status" = 'Paid' AND o."LandingPageId" IS NOT NULL
                  AND o."Email" IS NOT NULL AND btrim(o."Email") <> ''
            ) b
                ON b."LandingPageId" = lp."Id"
               AND NOT EXISTS (
                   SELECT 1 FROM marketing.unsubscribes u
                   WHERE u."CreatorId" = {creatorId} AND u."Email" = b."Email"
               )
            WHERE lp."CreatorId" = {creatorId} AND lp."Type" = 'Sales'
            GROUP BY lp."Id", lp."PublicId", lp."Title", lp."Status"
            HAVING lp."Status" <> 'Archived' OR COUNT(DISTINCT b."Email") > 0
            ORDER BY "RecipientCount" DESC, lp."Title", lp."Id"
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        return new CampaignAudiencesDto(
            new AllAudienceDto(allCount),
            landingPages.Select(r => new LandingPageAudienceDto(r.PublicId, r.Title, r.RecipientCount)).ToList(),
            products.Select(r => new ProductAudienceDto(r.PublicId, r.Name, r.RecipientCount)).ToList(),
            buyers.Select(r => new LandingPageAudienceDto(r.PublicId, r.Title, r.RecipientCount)).ToList());
    }

    public async Task<(int OpenedAny, int OpenedThroughResends)> GetNonOpenerReachAsync(int originalCampaignId, CancellationToken ct)
    {
        // Each person once: the opens across the original and its sent resends, grouped by (normalised) email.
        var reach = await _context.Database.SqlQuery<ReachRow>($"""
            WITH opens AS (
                SELECT r."Email", bool_or(c."Id" = {originalCampaignId}) AS "OpenedOriginal"
                FROM marketing.campaigns c
                JOIN marketing.campaign_recipients r ON r."CampaignId" = c."Id"
                WHERE (c."Id" = {originalCampaignId}
                       OR (c."SourceCampaignId" = {originalCampaignId} AND c."Status" = 'Queued'))
                  AND r."FirstOpenedAt" IS NOT NULL
                GROUP BY r."Email"
            )
            SELECT COUNT(*)::int AS "OpenedAny",
                   (COUNT(*) FILTER (WHERE NOT "OpenedOriginal"))::int AS "OpenedThroughResends"
            FROM opens
            """)
            .AsNoTracking()
            .SingleAsync(ct);

        return (reach.OpenedAny, reach.OpenedThroughResends);
    }

    /// <summary>
    /// The next non-opener resend of an original: its recipients whose email was delivered (outbox <c>Sent</c>;
    /// failed deliveries are what "retry failed" is for) and who have opened neither the original nor any sent
    /// resend of it, minus current unsubscribes. Someone who was in an earlier resend stays in only if that resend
    /// was delivered to them. Emails are compared normalised (recipient rows store them trimmed and lower-cased).
    /// Delivery is looked up per recipient on the outbox (<c>Purpose</c>, <c>CorrelationKey</c>, <c>Status</c>)
    /// index with the <c>{campaignPublicId}:{recipientId}</c> key; nothing runs per row in the application.
    /// Open tracking over-reports (Apple Mail Privacy Protection), so this under-reaches rather than over-reaches.
    /// </summary>
    private IQueryable<EmailRow> NonOpeners(int originalCampaignId, int creatorId) =>
        // The delivery checks are a LATERAL ... LIMIT 1 and a scalar subquery on purpose: an EXISTS would be
        // flattened into a join that reads every Sent broadcast message; these stay one index-only probe of the
        // (Purpose, CorrelationKey, Status) index per recipient, so the cost follows the recipients, not the outbox.
        _context.Database.SqlQuery<EmailRow>($"""
            SELECT r."Email" AS "Email"
            FROM marketing.campaigns o
            JOIN marketing.campaign_recipients r ON r."CampaignId" = o."Id"
            JOIN LATERAL (
                SELECT 1 FROM email.email_outbox_messages m
                WHERE m."Purpose" = 'CampaignBroadcast'
                  AND m."CorrelationKey" = o."PublicId"::text || ':' || r."Id"::text
                  AND m."Status" = 'Sent'
                LIMIT 1
            ) delivered ON true
            WHERE o."Id" = {originalCampaignId}
              AND o."CreatorId" = {creatorId}
              AND r."FirstOpenedAt" IS NULL
              AND NOT EXISTS (
                  SELECT 1 FROM marketing.unsubscribes u
                  WHERE u."CreatorId" = {creatorId} AND u."Email" = r."Email"
              )
              AND NOT EXISTS (
                  SELECT 1
                  FROM marketing.campaigns c
                  JOIN marketing.campaign_recipients rr ON rr."CampaignId" = c."Id" AND rr."Email" = r."Email"
                  WHERE c."SourceCampaignId" = o."Id"
                    AND c."Status" = 'Queued'
                    AND (rr."FirstOpenedAt" IS NOT NULL
                         OR (SELECT 1 FROM email.email_outbox_messages mm
                             WHERE mm."Purpose" = 'CampaignBroadcast'
                               AND mm."CorrelationKey" = c."PublicId"::text || ':' || rr."Id"::text
                               AND mm."Status" = 'Sent'
                             LIMIT 1) IS NULL)
              )
            """)
            .AsNoTracking();

    private static int RequireSource(int? sourceCampaignId) =>
        sourceCampaignId ?? throw new ArgumentException("A CampaignNonOpeners audience needs its source campaign.", nameof(sourceCampaignId));

    private sealed record ReachRow(int OpenedAny, int OpenedThroughResends);

    private sealed record EmailRow(string Email);

    private sealed record LandingPageAudienceRow(Guid PublicId, string Title, int RecipientCount);

    private sealed record ProductAudienceRow(Guid PublicId, string Name, int RecipientCount);

    private IQueryable<string> BuildAudienceQuery(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId)
    {
        if (audienceType == CampaignAudienceType.CampaignNonOpeners)
            throw new ArgumentOutOfRangeException(nameof(audienceType), audienceType, "Non-opener audiences are resolved by NonOpeners.");

        var unsubscribedEmails = _context.Set<Unsubscribe>()
            .AsNoTracking()
            .Where(u => u.CreatorId == creatorId)
            .Select(u => u.Email);

        IQueryable<string> captured;

        if (audienceType == CampaignAudienceType.All)
        {
            // Every contact the creator has captured, straight off the materialized summary table — no
            // re-aggregation of email_captures.
            captured = _context.Set<ContactSummary>()
                .AsNoTracking()
                .Where(cs => cs.CreatorId == creatorId)
                .Select(cs => cs.Email);
        }
        else if (audienceType == CampaignAudienceType.LandingPage)
        {
            captured = _context.Set<EmailCapture>()
                .AsNoTracking()
                .Where(ec => ec.LandingPageId == landingPageId)
                .Select(ec => ec.Email);
        }
        else
        {
            // Product audience = captures attributed directly to the product OR captures on any landing
            // page whose ProductId is this product — EmailCapture.ProductId is normally populated
            // straight from the landing page at capture time, but this covers both fill paths (e.g. older
            // rows, or the product assignment changing after capture) via a set union (dedupes both sides).
            var direct = _context.Set<EmailCapture>()
                .AsNoTracking()
                .Where(ec => ec.ProductId == productId)
                .Select(ec => ec.Email);

            var viaLandingPage = _context.Set<EmailCapture>()
                .AsNoTracking()
                .Join(
                    _context.Set<LandingPage>().AsNoTracking(),
                    ec => ec.LandingPageId,
                    lp => lp.Id,
                    (ec, lp) => new { ec.Email, lp.ProductId })
                .Where(x => x.ProductId == productId)
                .Select(x => x.Email);

            captured = direct.Union(viaLandingPage);
        }

        return captured.Distinct().Except(unsubscribedEmails);
    }
}
