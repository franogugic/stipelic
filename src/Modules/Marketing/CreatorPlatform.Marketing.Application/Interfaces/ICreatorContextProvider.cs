namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record MarketingCreatorContext(
    int CreatorId,
    Guid CreatorPublicId,
    string Name,
    string Slug,
    string? SupportEmail,
    string BrandName,
    string? LogoUrl,
    string PrimaryColor,
    string OwnerEmail);

public interface ICreatorContextProvider
{
    Task<MarketingCreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>Same context, resolved by internal creator id with no ownership check — for the
    /// background dispatch worker, which already has a trusted <c>CreatorId</c> off the Scheduled
    /// campaign row rather than a slug + HTTP caller.</summary>
    Task<MarketingCreatorContext?> GetByCreatorIdAsync(int creatorId, CancellationToken ct);

    /// <summary>Resolves a landing page's internal id, verifying it belongs to this creator. Null if not
    /// found or not owned — deliberately not distinguishing the two, to avoid leaking existence across
    /// creators (same principle as every other ownership check in this codebase).</summary>
    Task<int?> ResolveLandingPageIdAsync(int creatorId, Guid landingPagePublicId, CancellationToken ct);

    /// <summary>Same ownership contract as <see cref="ResolveLandingPageIdAsync"/>, for a product.</summary>
    Task<int?> ResolveProductIdAsync(int creatorId, Guid productPublicId, CancellationToken ct);

    /// <summary>Plan limit for <paramref name="limitKey"/> from the creator's active subscription. Null
    /// when the creator has no active subscription (caller decides how to treat that — e.g. reject).</summary>
    Task<int?> GetActivePlanLimitAsync(int creatorId, string limitKey, CancellationToken ct);

    /// <summary>Batch-resolves internal landing page ids to their public ids — one query for the whole
    /// set, not one per campaign, so list/detail rendering never does N+1.</summary>
    Task<Dictionary<int, Guid>> GetLandingPagePublicIdsAsync(IReadOnlyCollection<int> landingPageIds, CancellationToken ct);

    /// <summary>Same batch contract as <see cref="GetLandingPagePublicIdsAsync"/>, for products.</summary>
    Task<Dictionary<int, Guid>> GetProductPublicIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);
}
