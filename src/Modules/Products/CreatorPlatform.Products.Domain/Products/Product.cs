namespace CreatorPlatform.Products.Domain.Products;

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
        ProductType type,
        ProductStatus status,
        string? accessUrl,
        string? thumbnailUrl,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        Name = name;
        Description = description;
        PriceCents = priceCents;
        Type = type;
        Status = status;
        AccessUrl = accessUrl;
        ThumbnailUrl = thumbnailUrl;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static Product Create(
        int creatorId,
        string name,
        string? description,
        int priceCents,
        ProductType type,
        string? accessUrl,
        string? thumbnailUrl,
        DateTimeOffset createdAt)
    {
        return new Product(
            Guid.NewGuid(),
            creatorId,
            name,
            description,
            priceCents,
            type,
            ProductStatus.Draft,
            accessUrl,
            thumbnailUrl,
            createdAt);
    }

    public void Update(
        string name,
        string? description,
        int priceCents,
        ProductType type,
        string? accessUrl,
        string? thumbnailUrl,
        DateTimeOffset updatedAt)
    {
        Name = name;
        Description = description;
        PriceCents = priceCents;
        Type = type;
        AccessUrl = accessUrl;
        ThumbnailUrl = thumbnailUrl;
        UpdatedAt = updatedAt;
    }

    public void Publish(DateTimeOffset updatedAt)
    {
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

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int PriceCents { get; private set; }

    public ProductType Type { get; private set; }

    public ProductStatus Status { get; private set; }

    public string? AccessUrl { get; private set; }

    public string? ThumbnailUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
