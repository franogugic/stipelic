using CreatorPlatform.Media.Application.Dtos;
using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Application.Services;

public sealed class MediaUploadService : IMediaUploadService
{
    private readonly IBlobReferenceRepository _repository;
    private readonly IBlobStorageClient _blobStorageClient;
    private readonly IMediaUnitOfWork _unitOfWork;
    private readonly MediaOptions _options;

    public MediaUploadService(
        IBlobReferenceRepository repository,
        IBlobStorageClient blobStorageClient,
        IMediaUnitOfWork unitOfWork,
        IOptions<MediaOptions> options)
    {
        _repository = repository;
        _blobStorageClient = blobStorageClient;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<UploadUrlResponseDto> RequestUploadUrlAsync(
        int creatorId, MediaUploadPurpose purpose, string contentType, CancellationToken ct)
    {
        // Cheap allowlist check before ever touching the (comparatively expensive) SAS generation call.
        if (!_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                $"Unsupported content type '{contentType}'. Allowed: {string.Join(", ", _options.AllowedContentTypes)}.");
        }

        var now = DateTimeOffset.UtcNow;
        var blobPath = $"{purpose}/{creatorId}/{Guid.NewGuid()}.{ExtensionFor(contentType)}";
        var expiry = TimeSpan.FromMinutes(_options.SasExpiryMinutes);

        var uploadUrl = _blobStorageClient.GenerateUploadSasUri(blobPath, contentType, expiry);
        // The blob's permanent (non-SAS) URL is the same URI with the SAS query string stripped — no
        // second Azure call needed to learn it.
        var blobUrl = uploadUrl.GetLeftPart(UriPartial.Path);

        var reference = BlobReference.CreatePending(creatorId, purpose, blobUrl, contentType, now);
        await _repository.AddAsync(reference, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new UploadUrlResponseDto(uploadUrl.ToString(), blobUrl, now.Add(expiry));
    }

    public async Task<ConfirmUploadResponseDto> ConfirmUploadAsync(int creatorId, string blobUrl, CancellationToken ct)
    {
        var reference = await _repository.GetPendingByCreatorAndUrlAsync(creatorId, blobUrl, ct);
        if (reference is null)
            throw new NotFoundException("Upload reference not found.");

        // Check expiry before ever calling Azure — an expired Pending row may not even have a real blob
        // behind it, so there's no reason to spend a network call finding that out.
        var expiresAt = reference.CreatedAt.AddMinutes(_options.SasExpiryMinutes);
        if (expiresAt < DateTimeOffset.UtcNow)
            throw new ConflictException("Upload link expired, request a new one.");

        var properties = await _blobStorageClient.GetPropertiesAsync(blobUrl, ct);
        if (properties is null)
            throw new ConflictException("File was not uploaded.");

        var contentTypeAllowed = _options.AllowedContentTypes.Contains(properties.ContentType, StringComparer.OrdinalIgnoreCase);
        if (properties.ContentLength > _options.MaxFileSizeBytes || !contentTypeAllowed)
        {
            // Defense-in-depth: the client could have lied about contentType in the upload-url request, or
            // uploaded a larger file than declared — the real properties are the source of truth here.
            await _blobStorageClient.DeleteAsync(blobUrl, ct);
            throw new BadRequestException(
                $"File does not meet upload requirements (max {_options.MaxFileSizeBytes} bytes, types: {string.Join(", ", _options.AllowedContentTypes)}).");
        }

        reference.Confirm((int)properties.ContentLength, DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ConfirmUploadResponseDto(blobUrl);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "image/webp" => "webp",
        _ => "bin"
    };
}
