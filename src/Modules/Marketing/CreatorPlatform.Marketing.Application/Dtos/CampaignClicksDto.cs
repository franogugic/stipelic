namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>Click statistics of a campaign. <see cref="ClickRate"/> = unique clickers ÷ delivered (0–1, like the
/// open rate); 0 while nothing is delivered. Link scanners can pre-click links, so clicks over-report somewhat.</summary>
public sealed record CampaignClicksDto(int UniqueClickers, int TotalClicks, double ClickRate, List<CampaignLinkStatsDto> Links);

/// <summary><see cref="Kind"/> is <c>Button</c> or <c>Text</c>; <see cref="Label"/> is the CTA label of the button,
/// null for a text link (its visible text is the URL).</summary>
public sealed record CampaignLinkStatsDto(string Url, int Position, string Kind, string? Label, int UniqueClicks, int TotalClicks);
