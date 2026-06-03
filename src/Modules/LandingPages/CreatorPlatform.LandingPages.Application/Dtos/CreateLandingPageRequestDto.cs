namespace CreatorPlatform.LandingPages.Application.Dtos;

public sealed class CreateLandingPageRequestDto
{
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}
