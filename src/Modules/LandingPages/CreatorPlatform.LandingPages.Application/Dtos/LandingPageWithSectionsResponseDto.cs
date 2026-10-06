using System.Text.Json.Serialization;

namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class LandingPageWithSectionsResponseDto
{
    [JsonIgnore]
    public int Id { get; init; }

    [JsonIgnore]
    public int CreatorId { get; init; }

    // Internal id, not exposed over the wire — the public capture endpoint passes it to the capture service.
    [JsonIgnore]
    public int? ProductId { get; init; }

    public Guid? ProductPublicId { get; init; }
    public string? ProductName { get; init; }
    public string? ProductThumbnailUrl { get; init; }
    public int? ProductPriceCents { get; init; }

    public Guid PublicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? CustomDomain { get; init; }
    public List<LandingPageSectionResponseDto> Sections { get; init; } = [];

    /// <summary>Public page only: the creator's brand. Omitted on the owner endpoints.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PublicCreatorBrandDto? Creator { get; init; }

    /// <summary>Public page only: the product's type and currency. Omitted on the owner endpoints and when the
    /// page has no product.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PublicProductDto? Product { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
