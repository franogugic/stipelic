namespace CreatorPlatform.Products.Domain.Products;

/// <summary>The document a product delivers: a confirmed upload in the private container. <see cref="BlobPath"/> is the
/// path inside that container (never a public URL); <see cref="FileName"/> is the name buyers download it as.</summary>
public sealed record ProductFile(string BlobPath, string FileName, long SizeBytes, string ContentType, DateTimeOffset UploadedAt)
{
    public const int MaxBlobPathLength = 500;
    public const int MaxFileNameLength = 255;
    public const int MaxContentTypeLength = 100;

    public static ProductFile Create(string blobPath, string fileName, long sizeBytes, string contentType, DateTimeOffset uploadedAt)
    {
        if (string.IsNullOrWhiteSpace(blobPath) || blobPath.Length > MaxBlobPathLength)
            throw new ArgumentException("A product file needs its blob path.", nameof(blobPath));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("A product file needs a file name.", nameof(fileName));
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "A product file cannot be empty.");
        if (string.IsNullOrWhiteSpace(contentType) || contentType.Length > MaxContentTypeLength)
            throw new ArgumentException("A product file needs its content type.", nameof(contentType));

        var name = fileName.Trim();
        return new ProductFile(
            blobPath, name.Length > MaxFileNameLength ? name[..MaxFileNameLength] : name, sizeBytes, contentType, uploadedAt);
    }
}
