using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Domain.Campaigns;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>Resolves who a campaign's audience is. Ownership of the target landing page/product is NOT
/// re-checked here — callers resolve+verify the target's internal id once (via
/// <see cref="ICreatorContextProvider.ResolveLandingPageIdAsync"/> / ResolveProductIdAsync at campaign
/// creation time, or read it back off the already-validated Campaign row at send time) and pass it in
/// directly. Exactly one of <paramref name="landingPageId"/>/<paramref name="productId"/> is expected,
/// matching <paramref name="audienceType"/> — same invariant the Campaign entity itself enforces; for
/// <see cref="CampaignAudienceType.All"/> both are null. <see cref="CampaignAudienceType.LandingPageBuyers"/> takes the
/// landing page like <see cref="CampaignAudienceType.LandingPage"/>, but its recipients are the paid-order buyers of
/// that page (normalised email, minus unsubscribes).</summary>
public interface IAudienceService
{
    /// <summary>COUNT(DISTINCT email) — never materializes the audience list.</summary>
    Task<int> GetAudienceCountAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct);

    /// <summary>Materializes the full deduplicated, suppression-filtered audience — call only when
    /// actually about to send.</summary>
    Task<List<string>> GetAudienceEmailsAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId, CancellationToken ct);

    /// <summary>Keyset page over the same deduplicated, suppression-filtered audience — ordered by email,
    /// never OFFSET. Fetches one row past <paramref name="limit"/> to detect <c>HasMore</c>.</summary>
    Task<(List<string> Emails, bool HasMore)> GetAudiencePageAsync(
        CampaignAudienceType audienceType, int? landingPageId, int? productId, int creatorId,
        string? afterEmail, int limit, CancellationToken ct);

    /// <summary>Recipient counts for the All audience plus every Published landing page and Active product
    /// of the creator (and the buyers of every Sales page), with the same semantics as
    /// <see cref="GetAudienceCountAsync"/> (deduplicated, minus unsubscribes) — resolved with a fixed four queries,
    /// never one per target.</summary>
    Task<CampaignAudiencesDto> GetAudienceOverviewAsync(int creatorId, CancellationToken ct);
}
