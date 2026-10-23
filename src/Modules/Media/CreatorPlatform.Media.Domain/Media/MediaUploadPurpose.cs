namespace CreatorPlatform.Media.Domain.Media;

/// <summary>Also used verbatim as the first path segment of the generated blob path
/// (<c>{purpose}/{creatorId}/{guid}.{ext}</c>) and for audit logging — do not rename members lightly,
/// existing blob paths already contain the string form.</summary>
public enum MediaUploadPurpose
{
    ProductThumbnail,
    CreatorLogo,
    LandingPageHero,
    LandingPageProductImage,

    /// <summary>A product's document (F8): stored in the private container, delivered only through personal links.</summary>
    ProductFile
}
