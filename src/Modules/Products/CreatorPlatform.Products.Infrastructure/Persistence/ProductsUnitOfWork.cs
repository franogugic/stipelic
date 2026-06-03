using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Products.Infrastructure.Persistence;

public sealed class ProductsUnitOfWork : IProductsUnitOfWork
{
    private readonly CreatorPlatformDbContext _context;

    public ProductsUnitOfWork(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await _context.SaveChangesAsync(ct);
    }
}
