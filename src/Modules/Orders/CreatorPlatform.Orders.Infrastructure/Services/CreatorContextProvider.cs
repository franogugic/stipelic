using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Orders.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<LandingPageProductInfo?> GetProductInfoByLandingPageSlugAsync(
        string creatorSlug,
        string landingPageSlug,
        CancellationToken ct)
    {
        var rows = await _context.Database
            .SqlQuery<LandingPageProductInfoRow>($"""
                SELECT
                    c."Id" AS "CreatorId",
                    p."Id" AS "ProductId",
                    lp."Id" AS "LandingPageId",
                    p."Name" AS "ProductName",
                    p."PriceCents" AS "PriceCents",
                    c."DefaultCurrency" AS "Currency"
                FROM landing_pages.landing_pages lp
                JOIN creators.creators c ON c."Id" = lp."CreatorId"
                JOIN products.products p ON p."Id" = lp."ProductId"
                WHERE c."Slug"  = {creatorSlug}
                  AND lp."Slug" = {landingPageSlug}
                  AND lp."Status" = 'Published'
                  AND c."Status" != 'Disabled'
                LIMIT 1
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        if (rows.Count == 0)
            return null;

        var row = rows[0];
        return new LandingPageProductInfo(row.CreatorId, row.ProductId, row.LandingPageId, row.ProductName, row.PriceCents, row.Currency);
    }

    public async Task<string?> GetProductNameAsync(int productId, CancellationToken ct)
    {
        var rows = await _context.Database
            .SqlQuery<ProductNameRow>($"""
                SELECT p."Name" AS "Name"
                FROM products.products p
                WHERE p."Id" = {productId}
                LIMIT 1
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.Count == 0 ? null : rows[0].Name;
    }

    private sealed class ProductNameRow
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class LandingPageProductInfoRow
    {
        public int CreatorId { get; init; }
        public int ProductId { get; init; }
        public int LandingPageId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public int PriceCents { get; init; }
        public string Currency { get; init; } = string.Empty;
    }
}
