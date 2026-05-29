namespace CreatorPlatform.Creators.Domain.Creators;

public sealed class CreatorSettings
{
    private CreatorSettings()
    {
    }

    private CreatorSettings(
        Creator creator,
        string? supportEmail,
        string brandName,
        string? logoUrl,
        string primaryColor,
        string timezone,
        string language,
        DateTimeOffset createdAt)
    {
        Creator = creator;
        SupportEmail = supportEmail;
        BrandName = brandName;
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        Timezone = timezone;
        Language = language;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public static CreatorSettings Create(
        Creator creator,
        string? supportEmail,
        string brandName,
        string? logoUrl,
        string primaryColor,
        string timezone,
        string language,
        DateTimeOffset createdAt)
    {
        return new CreatorSettings(
            creator,
            supportEmail,
            brandName,
            logoUrl,
            primaryColor,
            timezone,
            language,
            createdAt);
    }

    public void Update(
        string? supportEmail,
        string brandName,
        string? logoUrl,
        string primaryColor,
        string timezone,
        string language,
        DateTimeOffset updatedAt)
    {
        SupportEmail = supportEmail;
        BrandName = brandName;
        LogoUrl = logoUrl;
        PrimaryColor = primaryColor;
        Timezone = timezone;
        Language = language;
        UpdatedAt = updatedAt;
    }

    public int CreatorId { get; private set; }

    public Creator Creator { get; private set; } = null!;

    public string? SupportEmail { get; private set; }

    public string BrandName { get; private set; } = string.Empty;

    public string? LogoUrl { get; private set; }

    public string PrimaryColor { get; private set; } = string.Empty;

    public string Timezone { get; private set; } = string.Empty;

    public string Language { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
