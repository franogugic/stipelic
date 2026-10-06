using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Orders.Infrastructure.Repositories;

public sealed class OrderReceiptReader : IOrderReceiptReader
{
    private readonly CreatorPlatformDbContext _context;

    public OrderReceiptReader(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<OrderReceiptRow?> GetBySessionIdAsync(string checkoutSessionId, CancellationToken ct)
    {
        // One row through the unique index on "StripeCheckoutSessionId".
        return (await _context.Database.SqlQuery<OrderReceiptRow>($"""
            SELECT
                o."PublicId"         AS "OrderPublicId",
                o."Status"           AS "Status",
                o."Name"             AS "BuyerName",
                o."Email"            AS "BuyerEmail",
                o."AmountCents"      AS "AmountCents",
                o."Currency"         AS "Currency",
                o."CreatedAt"        AS "CreatedAt",
                o."PaidAt"           AS "PaidAt",
                p."Name"             AS "ProductName",
                c."Name"             AS "CreatorName",
                c."Slug"             AS "CreatorSlug",
                s."BrandName"        AS "BrandName",
                s."PrimaryColor"     AS "BrandColor",
                s."LogoUrl"          AS "LogoUrl",
                s."SupportEmail"     AS "SupportEmail"
            FROM orders.orders o
            JOIN products.products p ON p."Id" = o."ProductId"
            JOIN creators.creators c ON c."Id" = o."CreatorId"
            LEFT JOIN creators.creator_settings s ON s."CreatorId" = c."Id"
            WHERE o."StripeCheckoutSessionId" = {checkoutSessionId}
            """)
            .AsNoTracking()
            .ToListAsync(ct))
            .SingleOrDefault();
    }
}
