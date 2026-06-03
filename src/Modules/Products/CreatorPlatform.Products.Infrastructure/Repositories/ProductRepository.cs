using CreatorPlatform.Products.Application.Interfaces;
using CreatorPlatform.Products.Domain.Products;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly CreatorPlatformDbContext _context;

    public ProductRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> ListByCreatorIdAsync(int creatorId, CancellationToken ct)
    {
        return await _context
            .Set<Product>()
            .AsNoTracking()
            .Where(p => p.CreatorId == creatorId && p.Status != ProductStatus.Archived)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Product?> GetByPublicIdAndCreatorIdForUpdateAsync(Guid publicId, int creatorId, CancellationToken ct)
    {
        return await _context
            .Set<Product>()
            .FirstOrDefaultAsync(p => p.PublicId == publicId && p.CreatorId == creatorId, ct);
    }

    public async Task AddAsync(Product product, CancellationToken ct)
    {
        await _context.Set<Product>().AddAsync(product, ct);
    }
}
