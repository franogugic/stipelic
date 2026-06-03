using CreatorPlatform.Api.Responses;
using CreatorPlatform.Auth.Application.Exceptions;
using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creators/{slug}/landing-pages")]
public sealed class LandingPagesController : ControllerBase
{
    private readonly ILandingPageService _landingPageService;
    private readonly ICurrentUserContext _currentUserContext;

    public LandingPagesController(
        ILandingPageService landingPageService,
        ICurrentUserContext currentUserContext)
    {
        _landingPageService = landingPageService;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<LandingPageResponseDto>>>> List(
        string slug,
        CancellationToken ct)
    {
        var user = GetAuthenticatedUser();
        var pages = await _landingPageService.ListAsync(slug, user.Id, ct);
        return Ok(ApiResponse<List<LandingPageResponseDto>>.Success(StatusCodes.Status200OK, "Landing pages loaded.", pages));
    }

    
    [HttpGet("{pageId:guid}")]
    public async Task<ActionResult<ApiResponse<LandingPageWithSectionsResponseDto>>> Get(
        string slug,
        Guid pageId,
        CancellationToken ct)
    {
        var user = GetAuthenticatedUser();
        var page = await _landingPageService.GetWithSectionsAsync(slug, pageId, user.Id, ct);
        return Ok(ApiResponse<LandingPageWithSectionsResponseDto>.Success(StatusCodes.Status200OK, "Landing page loaded.", page));
    }

    [HttpPost]
    [EnableRateLimiting("CreateLandingPage")]
    public async Task<ActionResult<ApiResponse<LandingPageResponseDto>>> Create(
        string slug,
        [FromBody] CreateLandingPageRequestDto request,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();
        var page = await _landingPageService.CreateAsync(slug, user.Id, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<LandingPageResponseDto>.Success(StatusCodes.Status201Created, "Landing page created.", page));
    }

    [HttpPost("{pageId:guid}/publish")]
    public async Task<ActionResult<ApiResponse<object>>> Publish(string slug, Guid pageId, CancellationToken ct)
    {
        var user = GetVerifiedUser();
        await _landingPageService.PublishAsync(slug, pageId, user.Id, ct);
        return Ok(ApiResponse<object>.Success(StatusCodes.Status200OK, "Landing page published.", null));
    }

    [HttpPost("{pageId:guid}/unpublish")]
    public async Task<ActionResult<ApiResponse<object>>> Unpublish(string slug, Guid pageId, CancellationToken ct)
    {
        var user = GetVerifiedUser();
        await _landingPageService.UnpublishAsync(slug, pageId, user.Id, ct);
        return Ok(ApiResponse<object>.Success(StatusCodes.Status200OK, "Landing page unpublished.", null));
    }

    [HttpDelete("{pageId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Archive(string slug, Guid pageId, CancellationToken ct)
    {
        var user = GetVerifiedUser();
        await _landingPageService.ArchiveAsync(slug, pageId, user.Id, ct);
        return Ok(ApiResponse<object>.Success(StatusCodes.Status200OK, "Landing page archived.", null));
    }

    [HttpPut("{pageId:guid}/editor")]
    [EnableRateLimiting("UpdateLandingPage")]
    public async Task<ActionResult<ApiResponse<LandingPageWithSectionsResponseDto>>> SaveEditor(
        string slug,
        Guid pageId,
        [FromBody] SaveLandingPageRequestDto request,
        CancellationToken ct)
    {
        var user = GetVerifiedUser();
        var page = await _landingPageService.SaveEditorAsync(slug, pageId, user.Id, request, ct);
        return Ok(ApiResponse<LandingPageWithSectionsResponseDto>.Success(StatusCodes.Status200OK, "Landing page saved.", page));
    }

    [HttpGet("section-templates")]
    public ActionResult<ApiResponse<List<SectionTemplateResponseDto>>> GetSectionTemplates(string slug)
    {
        _ = GetAuthenticatedUser();
        var templates = _landingPageService.GetSectionTemplates();
        return Ok(ApiResponse<List<SectionTemplateResponseDto>>.Success(StatusCodes.Status200OK, "Section templates loaded.", templates));
    }

    private Auth.Application.Dtos.CurrentUserDto GetAuthenticatedUser()
    {
        var user = _currentUserContext.User;
        if (user is null)
            throw new UnauthorizedException("Authentication is required.");
        return user;
    }

    private Auth.Application.Dtos.CurrentUserDto GetVerifiedUser()
    {
        var user = GetAuthenticatedUser();
        if (!user.IsEmailVerified)
            throw new EmailNotVerifiedException();
        return user;
    }
}
