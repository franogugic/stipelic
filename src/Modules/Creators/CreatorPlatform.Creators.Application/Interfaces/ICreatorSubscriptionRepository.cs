using CreatorPlatform.Creators.Domain.Creators;

namespace CreatorPlatform.Creators.Application.Interfaces;

public interface ICreatorSubscriptionRepository
{
    Task AddAsync(CreatorSubscription subscription, CancellationToken ct);

    Task<CreatorSubscription?> GetCurrentByCreatorIdAsync(int creatorId, CancellationToken ct);
}
