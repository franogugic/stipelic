namespace CreatorPlatform.Media.Application.Dtos;

public sealed record UploadUrlResponseDto(string UploadUrl, string BlobUrl, DateTimeOffset ExpiresAt);
