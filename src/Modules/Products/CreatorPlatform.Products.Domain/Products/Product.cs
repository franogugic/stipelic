namespace CreatorPlatform.Products.Domain.Products;

/// <summary>
/// A product is a document (F8): buyers of a paid product and sign-ups on a lead page get a personal link to download
/// its file. A price of 0 means a free product, given away for an email on a lead page. An <see cref="ProductStatus.Active"/>
/// product always has a file; a Draft may have none.
/// </summary>
public sealed class Product
{
    private Product()
    {
    }

    private Product(
        Guid publicId,
        int creatorId,
        string name,
        string? description,
        int priceCents,
        string? thumbnailUrl,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        Name = name;
        Description = description;
        PriceCents = priceCents;
        Status = ProductStatus.Draft;
        ThumbnailUrl = thumbnailUrl;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Product Create(
        int creatorId,
        string name,
        string? description,
        int priceCents,
        string? thumbnailUrl,
        DateTimeOffset createdAt)
    {
        return new Product(Guid.NewGuid(), creatorId, name, description, priceCents, thumbnailUrl, createdAt);
    }

    public void Update(
        string name,
        string? description,
        int priceCents,
        string? thumbnailUrl,
        DateTimeOffset updatedAt)
    {
        Name = name;
        Description = description;
        PriceCents = priceCents;
        ThumbnailUrl = thumbnailUrl;
        UpdatedAt = updatedAt;
    }

    /// <summary>Sets or replaces the document. Everyone with a link gets the newest file.</summary>
    public void AttachFile(ProductFile file, DateTimeOffset updatedAt)
    {
        FileBlobPath = file.BlobPath;
        FileName = file.FileName;
        FileSizeBytes = file.SizeBytes;
        FileContentType = file.ContentType;
        FileUploadedAt = file.UploadedAt;
        UpdatedAt = updatedAt;
    }

    /// <summary>Removes the document; only a product that is not Active can be without one.</summary>
    public void RemoveFile(DateTimeOffset updatedAt)
    {
        if (Status == ProductStatus.Active)
            throw new InvalidOperationException("An active product must keep its file. Unpublish it first.");

        FileBlobPath = null;
        FileName = null;
        FileSizeBytes = null;
        FileContentType = null;
        FileUploadedAt = null;
        UpdatedAt = updatedAt;
    }

    public void Publish(DateTimeOffset updatedAt)
    {
        if (!HasFile)
            throw new InvalidOperationException("A product needs a file before it can be active.");

        Status = ProductStatus.Active;
        UpdatedAt = updatedAt;
    }

    public void Unpublish(DateTimeOffset updatedAt)
    {
        Status = ProductStatus.Draft;
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset updatedAt)
    {
        Status = ProductStatus.Archived;
        UpdatedAt = updatedAt;
    }

    public void Restore(DateTimeOffset updatedAt)
    {
        if (Status != ProductStatus.Archived)
            throw new InvalidOperationException("Only archived products can be restored.");

        Status = ProductStatus.Draft;
        UpdatedAt = updatedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>0 = a free product (lead magnet); otherwise sold on Sales pages.</summary>
    public int PriceCents { get; private set; }

    public bool IsFree => PriceCents == 0;

    public ProductStatus Status { get; private set; }

    public string? ThumbnailUrl { get; private set; }

    /// <summary>Path of the document in the private container; null while the product has no file.</summary>
    public string? FileBlobPath { get; private set; }

    public string? FileName { get; private set; }

    public long? FileSizeBytes { get; private set; }

    public string? FileContentType { get; private set; }

    public DateTimeOffset? FileUploadedAt { get; private set; }

    public bool HasFile => FileBlobPath is not null;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
