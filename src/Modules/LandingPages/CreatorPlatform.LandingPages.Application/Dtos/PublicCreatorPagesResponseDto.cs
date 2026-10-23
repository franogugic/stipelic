namespace CreatorPlatform.LandingPages.Application.Dtos;

/// <summary>The creator's brand as a visitor sees it.</summary>
public sealed record PublicCreatorBrandDto(string Name, string? PrimaryColor, string? LogoUrl);

/// <summary>The product facts the public page needs beyond name, price and image.</summary>
/// <param name="IsFree">A free product is given away for an email (lead pages); a paid one is sold.</param>
/// <param name="Currency">"Eur" | "Usd" — the creator's selling currency.</param>
public sealed record PublicProductDto(bool IsFree, string Currency);

/// <summary>"More from {brand}": the creator's published pages, shown when a page is missing.</summary>
public sealed record PublicCreatorPagesResponseDto(PublicCreatorBrandDto Creator, List<PublicCreatorPageDto> Pages);

/// <param name="Type">"Sales" | "LeadGen".</param>
public sealed record PublicCreatorPageDto(
    string Title,
    string Slug,
    string Type,
    int? ProductPriceCents,
    string? Currency,
    string? ThumbnailUrl);
