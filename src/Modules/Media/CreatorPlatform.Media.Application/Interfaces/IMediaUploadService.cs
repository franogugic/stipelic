using CreatorPlatform.Media.Application.Dtos;
using CreatorPlatform.Media.Domain.Media;

namespace CreatorPlatform.Media.Application.Interfaces;

public interface IMediaUploadService
{
    /// <summary>Validates <paramref name="contentType"/> against the allowlist, creates a Pending
    /// <see cref="BlobReference"/> at a server-decided path, and returns a short-lived SAS upload URL.
    /// Throws <see cref="Shared.Application.Exceptions.BadRequestException"/> for a disallowed content
    /// type.</summary>
    /// <param name="fileName">Optional for <see cref="MediaUploadPurpose.ProductFile"/>: checked when given (blank → 400
    /// <c>FILE_NAME_REQUIRED</c>); the confirm may give it instead.</param>
    Task<UploadUrlResponseDto> RequestUploadUrlAsync(
        int creatorId, MediaUploadPurpose purpose, string contentType, CancellationToken ct, string? fileName = null);

    /// <summary>Verifies ownership, expiry, and the blob's real (server-read) size/content-type before
    /// flipping the reference to Confirmed. Deletes the blob and throws
    /// <see cref="Shared.Application.Exceptions.BadRequestException"/> if the real properties violate the
    /// size/content-type limits — for a product document also if its first bytes don't match its type. A product
    /// document answers an opaque file reference instead of a URL.</summary>
    /// <param name="fileName">For <see cref="MediaUploadPurpose.ProductFile"/>: the name buyers download it as. Required
    /// unless the upload-url request gave one (this one wins); ignored for images.</param>
    Task<ConfirmUploadResponseDto> ConfirmUploadAsync(int creatorId, string blobUrl, CancellationToken ct, string? fileName = null);
}
