using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Services;

public sealed class OrderContextProvider : IOrderContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public OrderContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<int, ProductRevenueDto>> GetProductRevenueByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        var rows = await _context.Set<Order>()
            .AsNoTracking()
            .Where(o => o.CreatorId == creatorId)
            .GroupBy(o => o.ProductId)
            .Select(g => new ProductRevenueDto(
                g.Key,
                g.Sum(o => o.Status == OrderStatus.Paid ? o.AmountCents : 0),
                g.Count(o => o.Status == OrderStatus.Paid)))
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.ProductId);
    }
}
