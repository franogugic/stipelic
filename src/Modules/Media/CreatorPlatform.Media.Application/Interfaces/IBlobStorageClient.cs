namespace CreatorPlatform.Media.Application.Interfaces;

/// <summary>Real properties of an already-uploaded blob, read back from Azure — never trust what the
/// client claimed in the upload-url request.</summary>
public sealed record BlobProperties(long ContentLength, string ContentType);

/// <summary>Thin wrapper around the Azure Blob Storage SDK — fakeable in tests, same pattern as
/// <c>IConnectAccountService</c>/<c>IPaymentCheckoutSessionService</c> for Stripe. Tests never call real
/// Azure.</summary>
public interface IBlobStorageClient
{
    /// <summary>Generates a write-only SAS URI for a client to PUT bytes directly to
    /// <paramref name="blobPath"/> — the server never proxies the upload.</summary>
    Uri GenerateUploadSasUri(string blobPath, string contentType, TimeSpan expiry);

    /// <summary>Null if the blob doesn't exist (the client never actually uploaded, or the SAS expired
    /// before they did).</summary>
    Task<BlobProperties?> GetPropertiesAsync(string blobUrl, CancellationToken ct);

    /// <summary>Best-effort delete — logs a warning on failure, never throws (called from a request path
    /// that's already rejecting the upload; a delete failure shouldn't turn into a 500 on top of that).</summary>
    Task DeleteAsync(string blobUrl, CancellationToken ct);

    /// <summary>Deletes the blob if it exists (a missing one is success) and THROWS when the delete fails, for callers that
    /// must not forget a blob they could not delete (the abandoned-upload cleanup keeps its row and retries).</summary>
    Task DeleteIfExistsAsync(string blobUrl, CancellationToken ct);
}
