namespace CreatorPlatform.Media.Application.Dtos;

public sealed class UploadUrlRequestDto
{
    public string Purpose { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
}
