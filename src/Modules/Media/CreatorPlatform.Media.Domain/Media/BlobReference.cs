namespace CreatorPlatform.Media.Domain.Media;

/// <summary>Ownership + integrity record for a direct-to-blob upload. Created as <see cref="BlobReferenceStatus.Pending"/>
/// the moment a SAS upload URL is issued; flipped to <see cref="BlobReferenceStatus.Confirmed"/> only after
/// the server has independently verified the blob's real size/content-type against the creator's own
/// upload — never trusts the client's claimed content type or the raw URL without this row proving the
/// path was actually issued to this creator. Also the data-model foundation for a future orphan-blob sweep
/// (a Pending row whose SAS has expired without ever being confirmed) — the sweep job itself is V2.</summary>
public sealed class BlobReference
{
    private BlobReference()
    {
    }

    private BlobReference(
        Guid publicId,
        int creatorId,
        MediaUploadPurpose purpose,
        string blobUrl,
        string blobPath,
        string contentType,
        string? fileName,
        DateTimeOffset createdAt)
    {
        PublicId = publicId;
        CreatorId = creatorId;
        Purpose = purpose;
        BlobUrl = blobUrl;
        BlobPath = blobPath;
        ContentType = contentType;
        FileName = fileName;
        Status = BlobReferenceStatus.Pending;
        CreatedAt = createdAt;
    }

    /// <param name="blobPath">The path inside its container (<c>{purpose}/{creatorId}/{guid}.{ext}</c>).</param>
    /// <param name="fileName">A product document's name when the upload-url request gave it; the confirm may set it.</param>
    public static BlobReference CreatePending(
        int creatorId,
        MediaUploadPurpose purpose,
        string blobUrl,
        string blobPath,
        string contentType,
        DateTimeOffset createdAt,
        string? fileName = null)
    {
        return new BlobReference(Guid.NewGuid(), creatorId, purpose, blobUrl, blobPath, contentType, fileName, createdAt);
    }

    /// <summary>Only valid from <see cref="BlobReferenceStatus.Pending"/> — a confirmed reference is
    /// immutable history, so a second confirm attempt on the same row is a bug, not a no-op.</summary>
    /// <param name="fileName">The name the creator's file had (product documents: the name buyers download it as); null
    /// keeps the one given at upload-url time.</param>
    public void Confirm(int sizeBytes, DateTimeOffset confirmedAt, string? fileName = null)
    {
        if (Status != BlobReferenceStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm a blob reference in status '{Status}'.");

        SizeBytes = sizeBytes;
        FileName = fileName ?? FileName;
        Status = BlobReferenceStatus.Confirmed;
        ConfirmedAt = confirmedAt;
    }

    public int Id { get; private set; }

    public Guid PublicId { get; private set; }

    public int CreatorId { get; private set; }

    public MediaUploadPurpose Purpose { get; private set; }

    public string BlobUrl { get; private set; } = string.Empty;

    /// <summary>Path inside the container; null on rows from before it was recorded.</summary>
    public string? BlobPath { get; private set; }

    /// <summary>The uploaded file's original name — set for product documents.</summary>
    public string? FileName { get; private set; }

    public string ContentType { get; private set; } = string.Empty;

    public int? SizeBytes { get; private set; }

    public BlobReferenceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }
}
