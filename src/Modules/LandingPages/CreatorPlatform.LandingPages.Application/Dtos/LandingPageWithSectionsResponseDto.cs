using System.Text.Json.Serialization;

namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class LandingPageWithSectionsResponseDto
{
    [JsonIgnore]
    public int Id { get; init; }

    [JsonIgnore]
    public int? ProductId { get; init; }

    public Guid PublicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? CustomDomain { get; init; }
    public List<LandingPageSectionResponseDto> Sections { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
