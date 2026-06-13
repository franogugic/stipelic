using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.LandingPages.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<CreatorContext?> GetBySlugForOwnerAsync(
        string slug,
        int ownerUserId,
        CancellationToken ct)
    {
        var row = await _context.Database
            .SqlQuery<CreatorContextRow>($"""
                SELECT
                    c."Id" AS "CreatorId",
                    COALESCE(
                        (
                            SELECT cpl."LimitValue"
                            FROM creators.creator_subscriptions cs
                            JOIN creators.creator_plan_limits cpl
                                ON cpl."PlanId" = cs."PlanId"
                               AND cpl."LimitKey" = 'max_landing_pages'
                            WHERE cs."CreatorId" = c."Id"
                              AND cs."Status" != 'Cancelled'
                            ORDER BY cs."CreatedAt" DESC
                            LIMIT 1
                        ),
                        1
                    ) AS "MaxLandingPages",
                    (
                        SELECT COUNT(*)::int
                        FROM landing_pages.landing_pages lp
                        WHERE lp."CreatorId" = c."Id"
                          AND lp."Status" != 'Archived'
                    ) AS "ActiveLandingPageCount"
                FROM creators.creators c
                WHERE c."Slug"        = {slug}
                  AND c."OwnerUserId" = {ownerUserId}
                  AND c."Status"     != 'Disabled'
                LIMIT 1
                """)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        return new CreatorContext(row.CreatorId, row.MaxLandingPages, row.ActiveLandingPageCount);
    }

    public async Task<int?> GetProductIdForCreatorAsync(int creatorId, Guid productPublicId, CancellationToken ct)
    {
        var id = await _context.Database
            .SqlQuery<int>($"""
                SELECT p."Id"
                FROM products.products p
                WHERE p."PublicId" = {productPublicId}
                  AND p."CreatorId" = {creatorId}
                LIMIT 1
                """)
            .ToListAsync(ct);

        return id.Count > 0 ? id[0] : null;
    }

    public async Task<ProductInfo?> GetProductInfoAsync(int productId, CancellationToken ct)
    {
        var rows = await _context.Database
            .SqlQuery<ProductInfoRow>($"""
                SELECT p."Name", p."PriceCents"
                FROM products.products p
                WHERE p."Id" = {productId}
                LIMIT 1
                """)
            .ToListAsync(ct);

        return rows.Count > 0 ? new ProductInfo(rows[0].Name, rows[0].PriceCents) : null;
    }

    private sealed class ProductInfoRow
    {
        public string Name { get; init; } = string.Empty;
        public int PriceCents { get; init; }
    }

    private sealed class CreatorContextRow
    {
        public int CreatorId { get; init; }
        public int MaxLandingPages { get; init; }
        public int ActiveLandingPageCount { get; init; }
    }
}
