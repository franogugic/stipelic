namespace CreatorPlatform.Media.Application.Interfaces;

/// <summary>The private container for product documents. Nothing in it is ever publicly readable: uploads go through a
/// write-only SAS, downloads through a short-lived read SAS for one blob. Fakeable in tests; tests never call Azure.</summary>
public interface IProductFileStorage
{
    Uri GenerateUploadSasUri(string blobPath, TimeSpan expiry);

    /// <summary>Null when the blob doesn't exist.</summary>
    Task<BlobProperties?> GetPropertiesAsync(string blobPath, CancellationToken ct);

    /// <summary>The first <paramref name="byteCount"/> bytes (fewer for a smaller file).</summary>
    Task<byte[]> ReadHeadAsync(string blobPath, int byteCount, CancellationToken ct);

    /// <summary>Best effort: logs a failure, never throws.</summary>
    Task DeleteAsync(string blobPath, CancellationToken ct);

    /// <summary>A read-only URL for one blob, valid for <paramref name="expiry"/>, that makes the browser download it as
    /// <paramref name="downloadFileName"/> (<c>Content-Disposition: attachment</c>).</summary>
    Uri GenerateDownloadSasUri(string blobPath, string downloadFileName, string contentType, TimeSpan expiry);
}
