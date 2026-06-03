using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.LandingPages.Application.Services;

public sealed partial class LandingPageService : ILandingPageService
{
    private const int TitleMaxLength = 100;
    private const int SlugMaxLength = 100;

    private readonly ILandingPageRepository _landingPageRepository;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly ILandingPagesUnitOfWork _unitOfWork;

    public LandingPageService(
        ILandingPageRepository landingPageRepository,
        ICreatorContextProvider creatorContextProvider,
        ILandingPagesUnitOfWork unitOfWork)
    {
        _landingPageRepository = landingPageRepository;
        _creatorContextProvider = creatorContextProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<LandingPageResponseDto> CreateAsync(
        string slug,
        int ownerUserId,
        CreateLandingPageRequestDto request,
        CancellationToken ct)
    {
        var (creatorId, maxLandingPages, activeLandingPageCount) = await GetCreatorContextAsync(slug, ownerUserId, ct);

        var title = NormalizeTitle(request.Title);
        var landingPageSlug = NormalizeSlug(request.Slug);
        var type = ParseType(request.Type);

        if (maxLandingPages >= 0 && activeLandingPageCount >= maxLandingPages)
            throw new BadRequestException(
                $"Your plan allows a maximum of {maxLandingPages} landing page(s). Archive existing pages or upgrade your plan.");

        if (await _landingPageRepository.SlugExistsForCreatorAsync(creatorId, landingPageSlug, ct))
            throw new ConflictException("A landing page with this URL already exists.");

        var now = DateTimeOffset.UtcNow;
        var landingPage = LandingPage.Create(creatorId, null, title, landingPageSlug, type, now);

        await _landingPageRepository.AddAsync(landingPage, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(landingPage);
    }

    private async Task<CreatorContext> GetCreatorContextAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var context = await _creatorContextProvider.GetBySlugForOwnerAsync(slug, ownerUserId, ct);
        if (context is null)
            throw new NotFoundException("Creator workspace not found.");
        return context;
    }

    private static string NormalizeTitle(string? value)
    {
        var title = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title))
            throw new BadRequestException("Title is required.");
        if (title.Length > TitleMaxLength)
            throw new BadRequestException($"Title cannot exceed {TitleMaxLength} characters.");
        return title;
    }

    private static string NormalizeSlug(string? value)
    {
        var slug = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(slug))
            throw new BadRequestException("URL slug is required.");
        if (slug.Length > SlugMaxLength)
            throw new BadRequestException($"URL slug cannot exceed {SlugMaxLength} characters.");
        if (!SlugRegex().IsMatch(slug))
            throw new BadRequestException("URL slug can only contain lowercase letters, numbers and hyphens.");
        return slug;
    }

    private static LandingPageType ParseType(string? value)
    {
        if (!Enum.TryParse<LandingPageType>(value, ignoreCase: true, out var type))
            throw new BadRequestException($"Invalid type. Valid values: {string.Join(", ", Enum.GetNames<LandingPageType>())}.");
        return type;
    }

    private static LandingPageResponseDto MapToDto(LandingPage lp) => new()
    {
        PublicId = lp.PublicId,
        Title = lp.Title,
        Slug = lp.Slug,
        Type = lp.Type.ToString(),
        Status = lp.Status.ToString(),
        ProductId = lp.ProductId,
        CustomDomain = lp.CustomDomain,
        CreatedAt = lp.CreatedAt,
        UpdatedAt = lp.UpdatedAt
    };

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial System.Text.RegularExpressions.Regex SlugRegex();
}
