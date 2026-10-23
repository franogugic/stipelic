using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Domain.Access;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Access.Infrastructure.Services;

public sealed class AccessGrantReader : IAccessGrantReader
{
    private readonly CreatorPlatformDbContext _context;

    public AccessGrantReader(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<AccessGrantView?> GetAsync(string token, CancellationToken ct)
    {
        return await (
            from g in _context.Set<AccessGrant>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on g.ProductId equals p.Id
            join c in _context.Set<Creator>().AsNoTracking() on g.CreatorId equals c.Id
            from s in _context.Set<CreatorSettings>().AsNoTracking().Where(s => s.CreatorId == c.Id).DefaultIfEmpty()
            from o in _context.Set<Order>().AsNoTracking().Where(o => (int?)o.Id == g.OrderId).DefaultIfEmpty()
            where g.Token == token
            select new AccessGrantView(
                g.RevokedAt,
                p.Name,
                p.FileName,
                p.FileSizeBytes,
                c.Name,
                s != null ? s.BrandName : null,
                s != null ? s.PrimaryColor : null,
                s != null ? s.LogoUrl : null,
                o != null ? o.PublicId : null,
                o != null ? o.PaidAt : null)
        ).FirstOrDefaultAsync(ct);
    }

    public async Task<AccessDownloadTarget?> RecordDownloadAsync(string token, DateTimeOffset downloadedAt, CancellationToken ct)
    {
        // One statement: the count, the download event (R6.2) and the file come from the same row lock, so a revoked
        // link or a product without a file is never counted. The event keeps the page the link came from.
        var rows = await _context.Database.SqlQuery<AccessDownloadTarget>($"""
            WITH bumped AS (
                UPDATE products.access_grants g
                SET "DownloadCount" = g."DownloadCount" + 1,
                    "LastDownloadedAt" = {downloadedAt}
                FROM products.products p
                WHERE g."Token" = {token}
                  AND g."RevokedAt" IS NULL
                  AND p."Id" = g."ProductId"
                  AND p."FileBlobPath" IS NOT NULL
                RETURNING g."Id" AS "GrantId", g."CreatorId", g."ProductId", g."OrderId", g."EmailCaptureId",
                          p."FileBlobPath", p."FileName", p."FileContentType"
            ),
            logged AS (
                INSERT INTO products.download_events ("GrantId", "CreatorId", "ProductId", "LandingPageId", "DownloadedAt")
                SELECT b."GrantId", b."CreatorId", b."ProductId", COALESCE(o."LandingPageId", ec."LandingPageId"), {downloadedAt}
                FROM bumped b
                LEFT JOIN orders.orders o ON o."Id" = b."OrderId"
                LEFT JOIN analytics.email_captures ec ON ec."Id" = b."EmailCaptureId"
            )
            SELECT b."FileBlobPath" AS "BlobPath", b."FileName" AS "FileName", b."FileContentType" AS "ContentType"
            FROM bumped b
            """).ToListAsync(ct);

        return rows.SingleOrDefault();
    }
}
