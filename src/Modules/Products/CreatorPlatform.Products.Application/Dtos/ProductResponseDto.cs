namespace CreatorPlatform.Products.Application.Dtos;

public sealed class ProductResponseDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PriceCents { get; init; }
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? AccessUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int RevenueCents { get; init; }
    public int PaidOrderCount { get; init; }

    /// <summary>Paid orders per calendar month (UTC) for the last six months, oldest first, the current month last.
    /// Filled on the product list; all zeros on the single-product responses (create, update, restore).</summary>
    public int[] SalesTrend { get; init; } = new int[ProductSalesTrend.Months];
}
