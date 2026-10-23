namespace CreatorPlatform.Products.Application.Dtos;

public sealed class ProductResponseDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PriceCents { get; init; }

    /// <summary>Price 0: given away for an email on a lead page rather than sold.</summary>
    public bool IsFree { get; init; }

    public string Status { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }

    /// <summary>The document buyers and sign-ups download; null while the product has none (a Draft only).</summary>
    public ProductFileDto? File { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int RevenueCents { get; init; }
    public int PaidOrderCount { get; init; }

    /// <summary>Paid orders per calendar month (UTC) for the last six months, oldest first, the current month last.
    /// Filled on the product list; all zeros on the single-product responses (create, update, restore).</summary>
    public int[] SalesTrend { get; init; } = new int[ProductSalesTrend.Months];
}

/// <summary>A product's document as the creator sees it — never a URL (the file is private).</summary>
public sealed record ProductFileDto(string FileName, long SizeBytes, string ContentType, DateTimeOffset UploadedAt);
