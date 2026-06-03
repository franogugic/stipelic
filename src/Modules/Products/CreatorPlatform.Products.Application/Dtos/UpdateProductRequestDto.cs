namespace CreatorPlatform.Products.Application.Dtos;

public sealed class UpdateProductRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PriceCents { get; init; }
    public string Type { get; init; } = string.Empty;
    public string? AccessUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string Status { get; init; } = string.Empty;
}
