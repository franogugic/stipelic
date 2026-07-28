using CreatorPlatform.Media.Application.Dtos;
using CreatorPlatform.Media.Domain.Media;

namespace CreatorPlatform.Media.Application.Interfaces;

public interface IMediaUploadService
{
    /// <summary>Validates <paramref name="contentType"/> against the allowlist, creates a Pending
    /// <see cref="BlobReference"/> at a server-decided path, and returns a short-lived SAS upload URL.
    /// Throws <see cref="Shared.Application.Exceptions.BadRequestException"/> for a disallowed content
    /// type.</summary>
    Task<UploadUrlResponseDto> RequestUploadUrlAsync(int creatorId, MediaUploadPurpose purpose, string contentType, CancellationToken ct);

    /// <summary>Verifies ownership, expiry, and the blob's real (server-read) size/content-type before
    /// flipping the reference to Confirmed. Deletes the blob and throws
    /// <see cref="Shared.Application.Exceptions.BadRequestException"/> if the real properties violate the
    /// size/content-type limits.</summary>
    Task<ConfirmUploadResponseDto> ConfirmUploadAsync(int creatorId, string blobUrl, CancellationToken ct);
}
