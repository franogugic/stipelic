using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Orders.Infrastructure.Persistence;

public sealed class OrdersUnitOfWork : IOrdersUnitOfWork
{
    private readonly CreatorPlatformDbContext _context;

    public OrdersUnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
