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
        // One statement: the count and the file come from the same row lock, and a revoked link is never counted.
        var rows = await _context.Database.SqlQuery<AccessDownloadTarget>($"""
            UPDATE products.access_grants g
            SET "DownloadCount" = g."DownloadCount" + 1,
                "LastDownloadedAt" = {downloadedAt}
            FROM products.products p
            WHERE g."Token" = {token}
              AND g."RevokedAt" IS NULL
              AND p."Id" = g."ProductId"
              AND p."FileBlobPath" IS NOT NULL
            RETURNING p."FileBlobPath" AS "BlobPath", p."FileName" AS "FileName", p."FileContentType" AS "ContentType"
            """).ToListAsync(ct);

        return rows.SingleOrDefault();
    }
}
