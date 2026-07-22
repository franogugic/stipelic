using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Media.Infrastructure.Services;

public sealed class CreatorContextProvider : ICreatorContextProvider
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorContextProvider(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<int?> GetCreatorIdBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        return await _context.Set<Creator>()
            .AsNoTracking()
            .Where(c => c.Slug == slug && c.OwnerUserId == ownerUserId && c.Status != CreatorStatus.Disabled)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
    }
}
