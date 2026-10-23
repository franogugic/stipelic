namespace CreatorPlatform.Creators.Application.Interfaces;

/// <summary>Drops a workspace's cached read models after a change the Creators module makes on its own (e.g. a plan
/// change from a Stripe webhook). Implemented by the host, which can reach the other modules' caches. Call it after
/// the change is committed; it never throws for an unavailable cache.</summary>
public interface ICreatorCacheInvalidator
{
    Task InvalidateAsync(int creatorId, CancellationToken ct);
}
