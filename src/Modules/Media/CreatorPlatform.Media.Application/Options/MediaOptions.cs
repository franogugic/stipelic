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
}
