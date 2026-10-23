namespace CreatorPlatform.Products.Application.Dtos;

public sealed class ProductResponseDto
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>0 = a free product, given away for an email on lead pages instead of sold.</summary>
    public int PriceCents { get; init; }

    public string Status { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }

    // The document buyers and sign-ups download — never a URL (the file is private). All null while the product has none
    // (a Draft only).
    public string? FileName { get; init; }
    public long? FileSizeBytes { get; init; }
    public string? FileContentType { get; init; }
    public DateTimeOffset? FileUploadedAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int RevenueCents { get; init; }
    public int PaidOrderCount { get; init; }

    /// <summary>Paid orders per calendar month (UTC) for the last six months, oldest first, the current month last.
    /// Filled on the product list; all zeros on the single-product responses (create, update, restore).</summary>
    public int[] SalesTrend { get; init; } = new int[ProductSalesTrend.Months];

    // Free products on the product list only (null otherwise): sign-ups on the lead pages that give the product away.

    /// <summary>All time.</summary>
    public int? SignupCount { get; init; }

    /// <summary>In the creator's current local month (their timezone).</summary>
    public int? SignupsThisMonth { get; init; }

    /// <summary>Per local calendar month for the last six months, oldest first, the current month last.</summary>
    public int[]? SignupsTrend { get; init; }
}
