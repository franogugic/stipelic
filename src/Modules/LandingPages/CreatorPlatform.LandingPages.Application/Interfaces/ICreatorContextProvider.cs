namespace CreatorPlatform.LandingPages.Application.Interfaces;

public sealed record CreatorContext(int CreatorId, int MaxLandingPages, int ActiveLandingPageCount);

public interface ICreatorContextProvider
{
    Task<CreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);
}
