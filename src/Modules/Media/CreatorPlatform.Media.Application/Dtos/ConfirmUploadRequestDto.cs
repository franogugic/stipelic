namespace CreatorPlatform.Media.Application.Dtos;

public sealed class ConfirmUploadRequestDto
{
    public string BlobUrl { get; init; } = string.Empty;

    /// <summary>The picked file's name — required for a <c>ProductFile</c> (buyers download it under this name).</summary>
    public string? FileName { get; init; }
}
