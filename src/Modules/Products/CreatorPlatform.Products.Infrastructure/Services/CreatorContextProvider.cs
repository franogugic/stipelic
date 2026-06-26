using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private const string MaxProductsLimitKey = "max_products";

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
                               AND cpl."LimitKey" = 'max_products'
                            WHERE cs."CreatorId" = c."Id"
                              AND cs."Status" != 'Cancelled'
                            ORDER BY cs."CreatedAt" DESC
                            LIMIT 1
                        ),
                        1
                    ) AS "MaxProducts",
                    (
                        SELECT COUNT(*)::int
                        FROM products.products p
                        WHERE p."CreatorId" = c."Id"
                          AND p."Status" != 'Archived'
                    ) AS "ActiveProductCount"
                FROM creators.creators c
                WHERE c."Slug"        = {slug}
                  AND c."OwnerUserId" = {ownerUserId}
                  AND c."Status"     != 'Disabled'
                LIMIT 1
                """)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (row is null) return null;

        return new CreatorContext(row.CreatorId, row.MaxProducts, row.ActiveProductCount);
    }

    private sealed class CreatorContextRow
    {
        public int CreatorId { get; init; }
        public int MaxProducts { get; init; }
        public int ActiveProductCount { get; init; }
    }
}
