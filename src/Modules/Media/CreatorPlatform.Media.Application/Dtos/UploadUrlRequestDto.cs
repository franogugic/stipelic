namespace CreatorPlatform.Media.Application.Dtos;

public sealed class UploadUrlRequestDto
{
    public string Purpose { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;

    /// <summary>The file's name — required for a <c>ProductFile</c> (buyers download it under this name).</summary>
    public string? FileName { get; init; }
}
