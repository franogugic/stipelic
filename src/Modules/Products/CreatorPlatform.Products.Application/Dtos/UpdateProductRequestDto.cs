namespace CreatorPlatform.Products.Application.Dtos;

public sealed class UpdateProductRequestDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }

    /// <summary>0 = a free product. A product used by a lead page stays free, one used by a Sales page stays paid.</summary>
    public int PriceCents { get; init; }

    public string? ThumbnailUrl { get; init; }

    /// <summary>A new document (the reference of a confirmed <c>ProductFile</c> upload) replacing the current one; null keeps
    /// the current file. Everyone with a link gets the new file.</summary>
    public Guid? FileReference { get; init; }

    /// <summary>Removes the document (not for an Active product). Ignored when <see cref="FileReference"/> is set.</summary>
    public bool RemoveFile { get; init; }

    /// <summary>Active or Draft; missing or empty keeps the current status. Archived goes through the archive endpoint.</summary>
    public string? Status { get; init; }
}
