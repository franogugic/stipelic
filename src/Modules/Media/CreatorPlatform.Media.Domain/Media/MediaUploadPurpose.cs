namespace CreatorPlatform.Media.Domain.Media;

/// <summary>Also used verbatim as the first path segment of the generated blob path
/// (<c>{purpose}/{creatorId}/{guid}.{ext}</c>) and for audit logging — do not rename members lightly,
/// existing blob paths already contain the string form.</summary>
public enum MediaUploadPurpose
{
    ProductThumbnail,
    CreatorLogo,
    LandingPageHero,
    LandingPageProductImage
}
