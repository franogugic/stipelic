using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Orders.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly CreatorPlatformDbContext _context;

    public OrderRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Order order, CancellationToken ct)
    {
        await _context.Set<Order>().AddAsync(order, ct);
    }

    public async Task<Order?> GetByStripeCheckoutSessionIdAsync(string stripeCheckoutSessionId, CancellationToken ct)
    {
        return await _context
            .Set<Order>()
            .FirstOrDefaultAsync(o => o.StripeCheckoutSessionId == stripeCheckoutSessionId, ct);
    }
}
