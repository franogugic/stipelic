using CreatorPlatform.Media.Application.Dtos;
using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Application.Services;

public sealed class MediaUploadService : IMediaUploadService
{
    public const string FileRejectedCode = "FILE_REJECTED";
    public const string FileNameRequiredCode = "FILE_NAME_REQUIRED";
    public const string UploadLimitReachedCode = "UPLOAD_LIMIT_REACHED";
    private const int MaxFileNameLength = 255;

    private readonly IBlobReferenceRepository _repository;
    private readonly IBlobStorageClient _blobStorageClient;
    private readonly IProductFileStorage _productFileStorage;
    private readonly IMediaUnitOfWork _unitOfWork;
    private readonly MediaOptions _options;

    public MediaUploadService(
        IBlobReferenceRepository repository,
        IBlobStorageClient blobStorageClient,
        IProductFileStorage productFileStorage,
        IMediaUnitOfWork unitOfWork,
        IOptions<MediaOptions> options)
    {
        _repository = repository;
        _blobStorageClient = blobStorageClient;
        _productFileStorage = productFileStorage;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<UploadUrlResponseDto> RequestUploadUrlAsync(
        int creatorId, MediaUploadPurpose purpose, string contentType, CancellationToken ct, string? fileName = null)
    {
        var isProductFile = purpose == MediaUploadPurpose.ProductFile;
        var allowed = isProductFile ? _options.ProductFileContentTypes : _options.AllowedContentTypes;

        // Cheap allowlist check before ever touching the (comparatively expensive) SAS generation call.
        if (!allowed.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BadRequestException(
                $"Unsupported content type '{contentType}'. Allowed: {string.Join(", ", allowed)}.");
        }

        // A product file's name may come now or with the confirm; one given now must be a real name.
        var name = isProductFile && fileName is not null ? NormalizeFileName(fileName) : null;
        var now = DateTimeOffset.UtcNow;

        // The daily budget: every link can put a blob in storage. A soft limit — two requests racing at the edge may
        // both pass; the per-IP RequestMediaUpload rate limit bounds that.
        var issued = await _repository.CountIssuedSinceAsync(creatorId, now.AddDays(-1), ct);
        if (issued >= _options.DailyUploadLimit)
        {
            throw new ConflictException(
                $"Upload limit reached: at most {_options.DailyUploadLimit} uploads in 24 hours. Try again later.",
                UploadLimitReachedCode,
                new { used = issued, limit = _options.DailyUploadLimit });
        }

        var extension = isProductFile ? ProductFileSignatures.ExtensionFor(contentType) : ExtensionFor(contentType);
        var blobPath = $"{purpose}/{creatorId}/{Guid.NewGuid()}.{extension}";
        var expiry = TimeSpan.FromMinutes(_options.SasExpiryMinutes);

        var uploadUrl = isProductFile
            ? _productFileStorage.GenerateUploadSasUri(blobPath, expiry)
            : _blobStorageClient.GenerateUploadSasUri(blobPath, contentType, expiry);
        // The blob's permanent (non-SAS) URL is the same URI with the SAS query string stripped — no
        // second Azure call needed to learn it. For a product document it is only an identifier for confirm: the
        // container is private, so the URL itself reads nothing.
        var blobUrl = uploadUrl.GetLeftPart(UriPartial.Path);

        var reference = BlobReference.CreatePending(creatorId, purpose, blobUrl, blobPath, contentType.ToLowerInvariant(), now, name);
        await _repository.AddAsync(reference, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new UploadUrlResponseDto(uploadUrl.ToString(), blobUrl, now.Add(expiry));
    }

    public async Task<ConfirmUploadResponseDto> ConfirmUploadAsync(int creatorId, string blobUrl, CancellationToken ct, string? fileName = null)
    {
        var reference = await _repository.GetPendingByCreatorAndUrlAsync(creatorId, blobUrl, ct);
        if (reference is null)
            throw new NotFoundException("Upload reference not found.");

        // Check expiry before ever calling Azure — an expired Pending row may not even have a real blob
        // behind it, so there's no reason to spend a network call finding that out.
        var expiresAt = reference.CreatedAt.AddMinutes(_options.SasExpiryMinutes);
        if (expiresAt < DateTimeOffset.UtcNow)
            throw new ConflictException("Upload link expired, request a new one.");

        if (reference.Purpose == MediaUploadPurpose.ProductFile)
            return await ConfirmProductFileAsync(reference, NormalizeFileName(fileName ?? reference.FileName), ct);

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

        return new ConfirmUploadResponseDto { Url = blobUrl };
    }

    /// <summary>
    /// A product document is accepted only if the blob really is what it claims: its real (server-read) type is
    /// allowed and the same as declared, its size is within the limit, and its first bytes match the type
    /// (<see cref="ProductFileSignatures"/>). Anything else is deleted and refused. The answer is an opaque reference,
    /// never a URL.
    /// </summary>
    private async Task<ConfirmUploadResponseDto> ConfirmProductFileAsync(BlobReference reference, string fileName, CancellationToken ct)
    {
        var blobPath = reference.BlobPath!;
        var properties = await _productFileStorage.GetPropertiesAsync(blobPath, ct);
        if (properties is null)
            throw new ConflictException("File was not uploaded.");

        var contentType = properties.ContentType?.ToLowerInvariant() ?? string.Empty;
        var acceptable =
            properties.ContentLength > 0
            && properties.ContentLength <= _options.ProductFileMaxSizeBytes
            && contentType == reference.ContentType
            && _options.ProductFileContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase)
            && ProductFileSignatures.Matches(
                contentType, await _productFileStorage.ReadHeadAsync(blobPath, ProductFileSignatures.HeadLength, ct));

        if (!acceptable)
        {
            await _productFileStorage.DeleteAsync(blobPath, ct);
            throw new BadRequestException(
                $"The file must be a PDF, EPUB or ZIP of at most {_options.ProductFileMaxSizeBytes / 1_048_576} MB.", FileRejectedCode);
        }

        reference.Confirm((int)properties.ContentLength, DateTimeOffset.UtcNow, fileName);
        await _unitOfWork.SaveChangesAsync(ct);

        return new ConfirmUploadResponseDto
        {
            FileReference = reference.PublicId,
            FileName = fileName,
            FileSizeBytes = properties.ContentLength,
            ContentType = contentType,
        };
    }

    private static string NormalizeFileName(string? fileName)
    {
        // Only the name: no directories, no control characters.
        var name = Path.GetFileName(fileName?.Replace('\\', '/') ?? string.Empty).Trim();
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());
        if (name.Length == 0)
            throw new BadRequestException("A product file needs its file name.", FileNameRequiredCode);

        return name.Length > MaxFileNameLength ? name[..MaxFileNameLength] : name;
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "image/webp" => "webp",
        _ => "bin"
    };
}
