using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>Resolves who a campaign's audience is. Ownership of the target landing page/product is NOT
/// re-checked here — callers resolve+verify the target's internal id once (via
/// <see cref="ICreatorContextProvider.ResolveLandingPageIdAsync"/> / ResolveProductIdAsync at campaign
/// creation time, or read it back off the already-validated Campaign row at send time) and pass it in
/// directly. Exactly one of <paramref name="landingPageId"/>/<paramref name="productId"/> is expected,
/// matching <paramref name="audienceType"/> — same invariant the Campaign entity itself enforces.</summary>
public interface IAudienceService
{
    /// <summary>COUNT(DISTINCT email) — never materializes the audience list.</summary>
    Task<int> GetAudienceCountAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct);

    /// <summary>Materializes the full deduplicated, suppression-filtered audience — call only when
    /// actually about to send.</summary>
    Task<List<string>> GetAudienceEmailsAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct);
}
