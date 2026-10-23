namespace CreatorPlatform.Access.Application.Interfaces;

/// <summary>A personal link as the download page sees it.</summary>
public sealed record AccessGrantView(
    bool IsRevoked,
    string ProductName,
    string? FileName,
    long? FileSizeBytes,
    string CreatorName,
    string? BrandName,
    string? BrandColor,
    string? LogoUrl,
    Guid? OrderPublicId,
    DateTimeOffset? PaidAt);

/// <summary>The product's current file, which a download hands out.</summary>
public sealed record AccessDownloadTarget(string BlobPath, string FileName, string ContentType);

public interface IAccessGrantReader
{
    /// <summary>Null for an unknown token.</summary>
    Task<AccessGrantView?> GetAsync(string token, CancellationToken ct);

    /// <summary>For a link that is on and whose product has a file: counts the download (one statement, so parallel
    /// downloads all count) and returns the file. Null otherwise.</summary>
    Task<AccessDownloadTarget?> RecordDownloadAsync(string token, DateTimeOffset downloadedAt, CancellationToken ct);
}
