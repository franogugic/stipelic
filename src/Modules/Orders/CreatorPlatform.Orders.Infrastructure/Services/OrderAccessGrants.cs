using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Products.Domain.Access;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Orders.Infrastructure.Services;

public sealed class OrderAccessGrants : IOrderAccessGrants
{
    private readonly CreatorPlatformDbContext _context;

    public OrderAccessGrants(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public string CreateForPaidOrder(int creatorId, int productId, int orderId, string buyerEmail, DateTimeOffset createdAt)
    {
        var grant = AccessGrant.ForOrder(creatorId, productId, orderId, buyerEmail, createdAt);
        _context.Set<AccessGrant>().Add(grant);
        return grant.Token;
    }

    public async Task RevokeForOrderAsync(int orderId, DateTimeOffset revokedAt, CancellationToken ct)
    {
        await _context.Set<AccessGrant>()
            .Where(g => g.OrderId == orderId && g.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.RevokedAt, revokedAt), ct);
    }
}
