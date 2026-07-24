using CreatorPlatform.Analytics.Domain.EmailCaptures;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Domain.Campaigns;
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
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct)
    {
        return await BuildAudienceQuery(audienceType, landingPageId, productId, creatorId).CountAsync(ct);
    }

    public async Task<List<string>> GetAudienceEmailsAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct)
    {
        return await BuildAudienceQuery(audienceType, landingPageId, productId, creatorId).ToListAsync(ct);
    }

    public async Task<(List<string> Emails, bool HasMore)> GetAudiencePageAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId,
        string? afterEmail, int limit, CancellationToken ct)
    {
        var after = afterEmail?.Trim().ToLowerInvariant() ?? string.Empty;
        var fetchLimit = limit + 1;

        // Raw SQL keyset scan (same pattern as ContactsRepository.SearchAsync) — Npgsql's EF provider does
        // not translate string comparison after a LINQ Distinct/Union, so this can't be a plain IQueryable
        // WHERE+OrderBy tacked onto BuildAudienceQuery.
        var rows = audienceType == CampaignAudienceType.LandingPage
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

    private sealed record EmailRow(string Email);

    private IQueryable<string> BuildAudienceQuery(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId)
    {
        var unsubscribedEmails = _context.Set<Unsubscribe>()
            .AsNoTracking()
            .Where(u => u.CreatorId == creatorId)
            .Select(u => u.Email);

        IQueryable<string> captured;

        if (audienceType == CampaignAudienceType.LandingPage)
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
