namespace CreatorPlatform.Media.Application.Dtos;

public sealed class UploadUrlRequestDto
{
    public string Purpose { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;

    /// <summary>A <c>ProductFile</c>'s name (buyers download it under this name). Optional here: it can come with the
    /// confirm instead (the agreed contract), which then wins.</summary>
    public string? FileName { get; init; }
}
