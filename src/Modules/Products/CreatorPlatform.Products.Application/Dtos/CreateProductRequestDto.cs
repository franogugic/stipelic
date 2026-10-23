namespace CreatorPlatform.Products.Application.Dtos;

public sealed class CreateProductRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>0 = a free product, given away for an email on a lead page.</summary>
    public int PriceCents { get; init; }

    public string? ThumbnailUrl { get; init; }

    /// <summary>The document: the reference a confirmed <c>ProductFile</c> upload returned. Required for an Active product.</summary>
    public Guid? FileReference { get; init; }

    /// <summary>Active or Draft; missing or empty creates a Draft. Archived is rejected. Active needs a file.</summary>
    public string? Status { get; init; }
}
