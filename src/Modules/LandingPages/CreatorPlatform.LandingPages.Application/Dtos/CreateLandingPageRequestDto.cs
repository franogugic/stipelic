namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class CreateLandingPageRequestDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    /// <summary>Sales: the paid product it sells (required). LeadGen: the Active free product it gives away, or null
    /// for a plain newsletter sign-up.</summary>
    public Guid? ProductId { get; init; }
}
