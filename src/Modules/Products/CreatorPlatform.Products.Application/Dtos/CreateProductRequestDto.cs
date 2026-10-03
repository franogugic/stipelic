namespace CreatorPlatform.Products.Application.Dtos;

public sealed class CreateProductRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PriceCents { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? AccessUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    /// <summary>Active or Draft; missing or empty creates a Draft. Archived is rejected.</summary>
    public string? Status { get; init; }
}
