namespace CreatorPlatform.Media.Application.Interfaces;

public interface ICreatorContextProvider
{
    /// <summary>Resolves the internal creator id for an owner-scoped request. Null if the slug doesn't
    /// exist, isn't owned by this user, or the creator is disabled.</summary>
    Task<int?> GetCreatorIdBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);
}
