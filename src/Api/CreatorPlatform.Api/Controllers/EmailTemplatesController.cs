using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Dtos;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/email-templates")]
public sealed class EmailTemplatesController : ControllerBase
{
    private readonly IEmailTemplateService _templateService;
    private readonly ICurrentUserContext _currentUserContext;

    public EmailTemplatesController(IEmailTemplateService templateService, ICurrentUserContext currentUserContext)
    {
        _templateService = templateService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<EmailTemplateDto>>>> List(string slug, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var templates = await _templateService.ListAsync(slug, currentUser.Id, ct);

        return Ok(ApiResponse<List<EmailTemplateDto>>.Success(
            StatusCodes.Status200OK,
            "Templates loaded.",
            templates));
    }

    [HttpGet("{templatePublicId:guid}")]
    public async Task<ActionResult<ApiResponse<EmailTemplateDto>>> Get(string slug, Guid templatePublicId, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var template = await _templateService.GetAsync(slug, currentUser.Id, templatePublicId, ct);

        return Ok(ApiResponse<EmailTemplateDto>.Success(
            StatusCodes.Status200OK,
            "Template loaded.",
            template));
    }

    [HttpPost]
    [EnableRateLimiting("MutateTemplate")]
    public async Task<ActionResult<ApiResponse<EmailTemplateDto>>> Create(
        string slug, [FromBody] SaveEmailTemplateRequestDto request, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var template = await _templateService.CreateAsync(slug, currentUser.Id, request, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<EmailTemplateDto>.Success(
            StatusCodes.Status201Created,
            "Template created.",
            template));
    }

    [HttpPut("{templatePublicId:guid}")]
    [EnableRateLimiting("MutateTemplate")]
    public async Task<ActionResult<ApiResponse<EmailTemplateDto>>> Update(
        string slug, Guid templatePublicId, [FromBody] SaveEmailTemplateRequestDto request, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var template = await _templateService.UpdateAsync(slug, currentUser.Id, templatePublicId, request, ct);

        return Ok(ApiResponse<EmailTemplateDto>.Success(
            StatusCodes.Status200OK,
            "Template updated.",
            template));
    }

    [HttpPost("{templatePublicId:guid}/archive")]
    [EnableRateLimiting("MutateTemplate")]
    public async Task<ActionResult<ApiResponse<EmailTemplateDto>>> Archive(string slug, Guid templatePublicId, CancellationToken ct)
    {
        var currentUser = GetVerifiedUser();

        var template = await _templateService.ArchiveAsync(slug, currentUser.Id, templatePublicId, ct);

        return Ok(ApiResponse<EmailTemplateDto>.Success(
            StatusCodes.Status200OK,
            "Template archived.",
            template));
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
