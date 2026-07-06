using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Domain.Products;
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
        return await (
            from o in _context.Set<Order>().AsNoTracking()
            join p in _context.Set<Product>().AsNoTracking() on o.ProductId equals p.Id
            where o.PublicId == orderPublicId && o.Status == OrderStatus.Paid
            select p.AccessUrl
        ).FirstOrDefaultAsync(ct);
    }
}
