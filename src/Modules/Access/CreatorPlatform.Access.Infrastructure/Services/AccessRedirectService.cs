using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Access.Infrastructure.Services;

public sealed class AccessRedirectService : IAccessRedirectService
{
    private readonly CreatorPlatformDbContext _context;

    public AccessRedirectService(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<string?> GetAccessUrlAsync(Guid orderPublicId, CancellationToken ct)
    {
        var rows = await _context.Database
            .SqlQuery<AccessUrlRow>($"""
                SELECT p."AccessUrl" AS "AccessUrl"
                FROM orders.orders o
                JOIN products.products p ON p."Id" = o."ProductId"
                WHERE o."PublicId" = {orderPublicId}
                  AND o."Status" = 'Paid'
                LIMIT 1
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        if (rows.Count == 0)
            return null;

        return rows[0].AccessUrl;
    }

    private sealed class AccessUrlRow
    {
        public string? AccessUrl { get; init; }
    }
}
