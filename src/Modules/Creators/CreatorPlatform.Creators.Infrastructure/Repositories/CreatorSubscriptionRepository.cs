using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Infrastructure.Persistence;

namespace CreatorPlatform.Creators.Infrastructure.Repositories;

public sealed class CreatorSubscriptionRepository : ICreatorSubscriptionRepository
{
    private readonly CreatorPlatformDbContext _context;

    public CreatorSubscriptionRepository(CreatorPlatformDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CreatorSubscription subscription, CancellationToken ct)
    {
        await _context.Set<CreatorSubscription>().AddAsync(subscription, ct);
    }
}
