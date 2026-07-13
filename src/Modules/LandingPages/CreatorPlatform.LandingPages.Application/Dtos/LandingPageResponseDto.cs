using System.Text.Json.Serialization;

namespace CreatorPlatform.LandingPages.Application.Dtos;

// A record (not a plain class) so the controller can merge in view counts via a `with` expression
// instead of re-listing every property by hand.
public sealed record LandingPageResponseDto
{
    // Internal id, not exposed over the wire — carried so callers that only need to resolve the page
    // (analytics, timeseries, captures) can use this lightweight DTO instead of the full with-sections one.
    [JsonIgnore]
    public int Id { get; init; }

    public Guid PublicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int? ProductId { get; init; }
    public string? CustomDomain { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public long TotalViews { get; init; }
    public long UniqueVisitors { get; init; }
}
