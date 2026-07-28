using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Media.Application.Dtos;
using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/media")]
public sealed class MediaController : ControllerBase
{
    private readonly IMediaUploadService _mediaUploadService;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly ICurrentUserContext _currentUserContext;

    public MediaController(
        IMediaUploadService mediaUploadService,
        ICreatorContextProvider creatorContextProvider,
        ICurrentUserContext currentUserContext)
    {
        _mediaUploadService = mediaUploadService;
        _creatorContextProvider = creatorContextProvider;
        _currentUserContext = currentUserContext;
    }

    [HttpPost("upload-url")]
    [EnableRateLimiting("RequestMediaUpload")]
    public async Task<ActionResult<ApiResponse<UploadUrlResponseDto>>> RequestUploadUrl(
        string slug, [FromBody] UploadUrlRequestDto request, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();
        var creatorId = await GetOwnedCreatorIdAsync(slug, currentUser.Id, ct);
        var purpose = ParsePurpose(request.Purpose);

        var response = await _mediaUploadService.RequestUploadUrlAsync(creatorId, purpose, request.ContentType, ct);

        return Ok(ApiResponse<UploadUrlResponseDto>.Success(
            StatusCodes.Status200OK,
            "Upload URL generated.",
            response));
    }

    [HttpPost("confirm")]
    [EnableRateLimiting("RequestMediaUpload")]
    public async Task<ActionResult<ApiResponse<ConfirmUploadResponseDto>>> Confirm(
        string slug, [FromBody] ConfirmUploadRequestDto request, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();
        var creatorId = await GetOwnedCreatorIdAsync(slug, currentUser.Id, ct);

        var response = await _mediaUploadService.ConfirmUploadAsync(creatorId, request.BlobUrl, ct);

        return Ok(ApiResponse<ConfirmUploadResponseDto>.Success(
            StatusCodes.Status200OK,
            "Upload confirmed.",
            response));
    }

    private async Task<int> GetOwnedCreatorIdAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var creatorId = await _creatorContextProvider.GetCreatorIdBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (creatorId is null)
            throw new NotFoundException("Creator workspace not found.");

        return creatorId.Value;
    }

    private static MediaUploadPurpose ParsePurpose(string value)
    {
        if (!Enum.TryParse<MediaUploadPurpose>(value, ignoreCase: true, out var purpose))
            throw new BadRequestException($"Invalid purpose. Valid values: {string.Join(", ", Enum.GetNames<MediaUploadPurpose>())}.");

        return purpose;
    }

    /// <summary>Returns the authenticated user or throws 401.</summary>
    private CurrentUserDto GetAuthenticatedUser()
    {
        var user = _currentUserContext.User;
        if (user is null)
            throw new UnauthorizedException("Authentication is required.");
        return user;
    }

    /// <summary>Returns the authenticated and email-verified user or throws 401/403.</summary>
    private CurrentUserDto GetVerifiedUser()
    {
        var user = GetAuthenticatedUser();
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        return user;
    }
}
