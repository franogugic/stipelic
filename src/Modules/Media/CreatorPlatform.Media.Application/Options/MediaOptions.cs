namespace CreatorPlatform.Media.Application.Options;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Azure Storage account connection string. Dev convenience only (same pattern as
    /// Stripe/ACS) — production goes through Key Vault.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    public string ContainerName { get; init; } = string.Empty;

    public int MaxFileSizeBytes { get; init; } = 5_242_880;

    public int SasExpiryMinutes { get; init; } = 15;

    public string[] AllowedContentTypes { get; init; } = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>The container for product documents. Must be private (no anonymous access): files are only ever read
    /// through a short-lived signed URL issued for a valid personal link.</summary>
    public string PrivateContainerName { get; init; } = string.Empty;

    /// <summary>The largest product document accepted (default 100 MB).</summary>
    public long ProductFileMaxSizeBytes { get; init; } = 104_857_600;

    public string[] ProductFileContentTypes { get; init; } = ["application/pdf", "application/epub+zip", "application/zip"];

    /// <summary>How long a download URL issued for a personal link stays valid.</summary>
    public int DownloadUrlExpiryMinutes { get; init; } = 10;
}
