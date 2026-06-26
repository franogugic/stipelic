namespace CreatorPlatform.Products.Application.Interfaces;

public sealed record CreatorContext(int CreatorId, int MaxProducts, int ActiveProductCount);

public interface ICreatorContextProvider
{
    Task<CreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);
}
