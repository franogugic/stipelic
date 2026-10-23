namespace CreatorPlatform.Media.Application.Dtos;

/// <summary>An image upload answers its public <see cref="Url"/>. A product document has no URL at all: it answers an
/// opaque <see cref="FileReference"/> (passed to the product save) with the file's name, size and type.</summary>
public sealed record ConfirmUploadResponseDto(
    string? Url,
    Guid? FileReference = null,
    string? FileName = null,
    long? SizeBytes = null,
    string? ContentType = null);
