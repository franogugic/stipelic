namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>Every audience a creator can target, each with its current recipient count (deduplicated,
/// unsubscribes already excluded) — landing pages and products are sorted by count, descending.</summary>
public sealed record CampaignAudiencesDto(
    AllAudienceDto All,
    List<LandingPageAudienceDto> LandingPages,
    List<ProductAudienceDto> Products);

public sealed record AllAudienceDto(int RecipientCount);

public sealed record LandingPageAudienceDto(Guid PublicId, string Title, int RecipientCount);

public sealed record ProductAudienceDto(Guid PublicId, string Name, int RecipientCount);
