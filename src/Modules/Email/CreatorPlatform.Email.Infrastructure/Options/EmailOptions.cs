namespace CreatorPlatform.Email.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string ConnectionString { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FrontendBaseUrl { get; init; } = string.Empty;
}
