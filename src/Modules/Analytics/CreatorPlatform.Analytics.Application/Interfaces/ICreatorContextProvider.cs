namespace CreatorPlatform.Analytics.Application.Interfaces;

public interface ICreatorContextProvider
{
    /// <summary>Plan limit for <paramref name="limitKey"/> from the creator's active subscription. Null
    /// when the creator has no active subscription.</summary>
    Task<int?> GetActivePlanLimitAsync(int creatorId, string limitKey, CancellationToken ct);
}
