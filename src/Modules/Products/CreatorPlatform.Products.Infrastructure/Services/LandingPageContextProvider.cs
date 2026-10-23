using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Services;

public sealed class LandingPageContextProvider : ILandingPageContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public LandingPageContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsUsedByPublishedPageAsync(int productId, CancellationToken ct)
    {
        return await _context.Set<LandingPage>()
            .AsNoTracking()
            .AnyAsync(lp => lp.ProductId == productId && lp.Status == LandingPageStatus.Published, ct);
    }

    public async Task<(bool LeadPage, bool SalesPage)> GetPageKindsUsingAsync(int productId, CancellationToken ct)
    {
        var types = await _context.Set<LandingPage>()
            .AsNoTracking()
            .Where(lp => lp.ProductId == productId && lp.Status != LandingPageStatus.Archived)
            .Select(lp => lp.Type)
            .Distinct()
            .ToListAsync(ct);

        return (types.Contains(LandingPageType.LeadGen), types.Contains(LandingPageType.Sales));
    }

    public async Task<Dictionary<int, ProductSignups>> GetProductSignupsAsync(int creatorId, DateTimeOffset now, CancellationToken ct)
    {
        // Captures on every lead page (archived ones too: the sign-ups happened) counted by the product the page gives
        // away; months are the creator's local ones (UTC without settings). One pass over the creator's captures.
        var rows = await _context.Database.SqlQuery<SignupRow>($"""
            WITH tz AS (
                SELECT COALESCE((SELECT s."Timezone" FROM creators.creator_settings s WHERE s."CreatorId" = {creatorId}), 'UTC') AS "Name"
            ),
            window_start AS (
                SELECT date_trunc('month', {now}::timestamptz AT TIME ZONE tz."Name") - interval '5 months' AS "Local", tz."Name"
                FROM tz
            ),
            captures AS (
                SELECT lp."ProductId" AS "ProductId",
                       date_trunc('month', ec."CapturedAt" AT TIME ZONE w."Name") AS "Month",
                       w."Local" AS "First"
                FROM analytics.email_captures ec
                JOIN landing_pages.landing_pages lp ON lp."Id" = ec."LandingPageId"
                CROSS JOIN window_start w
                WHERE lp."CreatorId" = {creatorId} AND lp."Type" = 'LeadGen' AND lp."ProductId" IS NOT NULL
            )
            SELECT "ProductId",
                   COUNT(*)::int AS "Total",
                   COUNT(*) FILTER (WHERE "Month" = "First")::int AS "M0",
                   COUNT(*) FILTER (WHERE "Month" = "First" + interval '1 month')::int AS "M1",
                   COUNT(*) FILTER (WHERE "Month" = "First" + interval '2 months')::int AS "M2",
                   COUNT(*) FILTER (WHERE "Month" = "First" + interval '3 months')::int AS "M3",
                   COUNT(*) FILTER (WHERE "Month" = "First" + interval '4 months')::int AS "M4",
                   COUNT(*) FILTER (WHERE "Month" = "First" + interval '5 months')::int AS "M5"
            FROM captures
            GROUP BY "ProductId"
            """)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.ProductId,
            r => new ProductSignups(r.Total, [r.M0, r.M1, r.M2, r.M3, r.M4, r.M5]));
    }

    private sealed record SignupRow(int ProductId, int Total, int M0, int M1, int M2, int M3, int M4, int M5);
}
