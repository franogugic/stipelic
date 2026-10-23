namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>Every audience a creator can target, each with its current recipient count (deduplicated,
/// unsubscribes already excluded) — landing pages, products and landing-page buyers are sorted by count, descending.
/// <paramref name="LandingPageBuyers"/>: the creator's Sales pages (paid orders only); a page that is archived is
/// listed only while it still has buyers.</summary>
public sealed record CampaignAudiencesDto(
    AllAudienceDto All,
    List<LandingPageAudienceDto> LandingPages,
    List<ProductAudienceDto> Products,
    List<LandingPageAudienceDto> LandingPageBuyers);

public sealed record AllAudienceDto(int RecipientCount);

public sealed record LandingPageAudienceDto(Guid PublicId, string Title, int RecipientCount);

public sealed record ProductAudienceDto(Guid PublicId, string Name, int RecipientCount);
