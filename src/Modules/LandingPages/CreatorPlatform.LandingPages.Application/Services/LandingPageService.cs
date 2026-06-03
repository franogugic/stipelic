using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Application.Templates;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.LandingPages.Application.Services;

public sealed partial class LandingPageService : ILandingPageService
{
    private const int TitleMaxLength = 100;
    private const int SlugMaxLength = 100;
    private const string DefaultBackgroundColor = "#ffffff";

    private readonly ILandingPageRepository _landingPageRepository;
    private readonly ILandingPageSectionRepository _sectionRepository;
    private readonly ICreatorContextProvider _creatorContextProvider;
    private readonly ILandingPagesUnitOfWork _unitOfWork;

    public LandingPageService(
        ILandingPageRepository landingPageRepository,
        ILandingPageSectionRepository sectionRepository,
        ICreatorContextProvider creatorContextProvider,
        ILandingPagesUnitOfWork unitOfWork)
    {
        _landingPageRepository = landingPageRepository;
        _sectionRepository = sectionRepository;
        _creatorContextProvider = creatorContextProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<LandingPageResponseDto> CreateAsync(
        string creatorSlug,
        int ownerUserId,
        CreateLandingPageRequestDto request,
        CancellationToken ct)
    {
        var (creatorId, maxLandingPages, activeLandingPageCount) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var title = NormalizeTitle(request.Title);
        var slug = NormalizeSlug(request.Slug);
        var type = ParseType(request.Type);

        if (maxLandingPages >= 0 && activeLandingPageCount >= maxLandingPages)
            throw new BadRequestException(
                $"Your plan allows a maximum of {maxLandingPages} landing page(s). Archive existing pages or upgrade your plan.");

        if (await _landingPageRepository.SlugExistsForCreatorAsync(creatorId, slug, ct))
            throw new ConflictException("A landing page with this URL already exists.");

        var now = DateTimeOffset.UtcNow;
        var landingPage = LandingPage.Create(creatorId, null, title, slug, type, now);

        await _landingPageRepository.AddAsync(landingPage, ct);

        var heroTemplate = SectionTemplates.GetDefault(LandingPageSectionType.Hero);
        var ctaTemplate = SectionTemplates.GetDefault(LandingPageSectionType.Cta);

        var hero = LandingPageSection.Create(landingPage, LandingPageSectionType.Hero, 0, heroTemplate.DefaultBackgroundColor, heroTemplate.ContentJson, now);
        var cta = LandingPageSection.Create(landingPage, LandingPageSectionType.Cta, 1, ctaTemplate.DefaultBackgroundColor, ctaTemplate.ContentJson, now);

        await _sectionRepository.AddAsync(hero, ct);
        await _sectionRepository.AddAsync(cta, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(landingPage);
    }

    public async Task<List<LandingPageResponseDto>> ListAsync(
        string creatorSlug,
        int ownerUserId,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var pages = await _landingPageRepository.ListByCreatorIdAsync(creatorId, ct);

        return pages.Select(MapToDto).ToList();
    }

    public async Task<LandingPageWithSectionsResponseDto> GetWithSectionsAsync(
        string creatorSlug,
        Guid landingPagePublicId,
        int ownerUserId,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var landingPage = await _landingPageRepository.GetByPublicIdAndCreatorIdForUpdateAsync(landingPagePublicId, creatorId, ct);
        if (landingPage is null)
            throw new NotFoundException("Landing page not found.");

        var sections = await _sectionRepository.ListByLandingPageIdAsync(landingPage.Id, ct);

        return MapToWithSectionsDto(landingPage, sections);
    }

    public async Task PublishAsync(string creatorSlug, Guid landingPagePublicId, int ownerUserId, CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var landingPage = await _landingPageRepository.GetByPublicIdAndCreatorIdForUpdateAsync(landingPagePublicId, creatorId, ct);
        if (landingPage is null)
            throw new NotFoundException("Landing page not found.");

        if (landingPage.Status == LandingPageStatus.Archived)
            throw new BadRequestException("Archived landing pages cannot be published.");

        if (landingPage.Status == LandingPageStatus.Published)
            return;

        landingPage.Publish(DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UnpublishAsync(string creatorSlug, Guid landingPagePublicId, int ownerUserId, CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var landingPage = await _landingPageRepository.GetByPublicIdAndCreatorIdForUpdateAsync(landingPagePublicId, creatorId, ct);
        if (landingPage is null)
            throw new NotFoundException("Landing page not found.");

        if (landingPage.Status != LandingPageStatus.Published)
            return;

        landingPage.Unpublish(DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(string creatorSlug, Guid landingPagePublicId, int ownerUserId, CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var landingPage = await _landingPageRepository.GetByPublicIdAndCreatorIdForUpdateAsync(landingPagePublicId, creatorId, ct);
        if (landingPage is null)
            throw new NotFoundException("Landing page not found.");

        if (landingPage.Status == LandingPageStatus.Archived)
            return;

        landingPage.Archive(DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<LandingPageWithSectionsResponseDto> SaveEditorAsync(
        string creatorSlug,
        Guid landingPagePublicId,
        int ownerUserId,
        SaveLandingPageRequestDto request,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var landingPage = await _landingPageRepository.GetByPublicIdAndCreatorIdForUpdateAsync(landingPagePublicId, creatorId, ct);
        if (landingPage is null)
            throw new NotFoundException("Landing page not found.");

        if (landingPage.Status == LandingPageStatus.Archived)
            throw new BadRequestException("Archived landing pages cannot be edited.");

        // Validate page info
        var title = NormalizeTitle(request.Title);
        var slug = NormalizeSlug(request.Slug);
        var type = ParseType(request.Type);

        if (slug != landingPage.Slug && await _landingPageRepository.SlugExistsForCreatorAsync(creatorId, slug, ct))
            throw new ConflictException("A landing page with this URL already exists.");

        // Validate sections structure
        if (request.Sections.Count < 2)
            throw new BadRequestException("Landing page must have at least a Hero and a CTA section.");

        var requestedTypes = request.Sections.Select(s => ParseSectionType(s.Type)).ToList();

        if (requestedTypes[0] != LandingPageSectionType.Hero)
            throw new BadRequestException("First section must be Hero.");

        if (requestedTypes[^1] != LandingPageSectionType.Cta)
            throw new BadRequestException("Last section must be CTA.");

        if (requestedTypes.Count(t => t == LandingPageSectionType.Hero) != 1)
            throw new BadRequestException("Landing page must have exactly one Hero section.");

        if (requestedTypes.Count(t => t == LandingPageSectionType.Cta) != 1)
            throw new BadRequestException("Landing page must have exactly one CTA section.");

        // Load existing sections
        var existingSections = await _sectionRepository.ListByLandingPageIdAsync(landingPage.Id, ct);
        var existingById = existingSections.ToDictionary(s => s.PublicId);

        var now = DateTimeOffset.UtcNow;

        // Update page info
        landingPage.Update(landingPage.ProductId, title, slug, type, now);

        // Determine sections to delete (existing ones not present in request)
        var requestedPublicIds = request.Sections
            .Where(s => s.PublicId.HasValue)
            .Select(s => s.PublicId!.Value)
            .ToHashSet();

        foreach (var existing in existingSections.Where(s => !requestedPublicIds.Contains(s.PublicId)))
            _sectionRepository.Remove(existing);

        // Upsert sections
        var resultSections = new List<LandingPageSection>();

        for (var i = 0; i < request.Sections.Count; i++)
        {
            var dto = request.Sections[i];
            var sectionType = requestedTypes[i];
            var backgroundColor = NormalizeColor(dto.BackgroundColor);
            var contentJson = NormalizeContentJson(dto.ContentJson);

            if (dto.PublicId.HasValue && existingById.TryGetValue(dto.PublicId.Value, out var existing))
            {
                existing.Update(i, backgroundColor, contentJson, now);
                resultSections.Add(existing);
            }
            else
            {
                var newSection = LandingPageSection.Create(landingPage, sectionType, i, backgroundColor, contentJson, now);
                await _sectionRepository.AddAsync(newSection, ct);
                resultSections.Add(newSection);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToWithSectionsDto(landingPage, resultSections);
    }

    public List<SectionTemplateResponseDto> GetSectionTemplates()
    {
        return SectionTemplates.All
            .Where(t => t.Type is not LandingPageSectionType.Hero and not LandingPageSectionType.Cta)
            .Select(t => new SectionTemplateResponseDto
            {
                Key = t.Key,
                Name = t.Name,
                Type = t.Type.ToString(),
                ContentJson = t.ContentJson,
                DefaultBackgroundColor = t.DefaultBackgroundColor
            })
            .ToList();
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

    private static string NormalizeColor(string? value)
    {
        var color = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(color))
            return DefaultBackgroundColor;
        if (!ColorRegex().IsMatch(color))
            throw new BadRequestException("Background color must be a valid hex color (e.g. #ffffff).");
        return color.ToLowerInvariant();
    }

    private static string NormalizeContentJson(string? value)
    {
        var json = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(json))
            throw new BadRequestException("Section content is required.");
        try
        {
            System.Text.Json.JsonDocument.Parse(json);
        }
        catch
        {
            throw new BadRequestException("Section content must be valid JSON.");
        }
        return json;
    }

    private static LandingPageType ParseType(string? value)
    {
        if (!Enum.TryParse<LandingPageType>(value, ignoreCase: true, out var type))
            throw new BadRequestException($"Invalid type. Valid values: {string.Join(", ", Enum.GetNames<LandingPageType>())}.");
        return type;
    }

    private static LandingPageSectionType ParseSectionType(string? value)
    {
        if (!Enum.TryParse<LandingPageSectionType>(value, ignoreCase: true, out var type))
            throw new BadRequestException($"Invalid section type. Valid values: {string.Join(", ", Enum.GetNames<LandingPageSectionType>())}.");
        return type;
    }

    private static bool IsLockedSection(LandingPageSectionType type)
        => type is LandingPageSectionType.Hero or LandingPageSectionType.Cta;

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

    private static LandingPageWithSectionsResponseDto MapToWithSectionsDto(LandingPage lp, List<LandingPageSection> sections) => new()
    {
        PublicId = lp.PublicId,
        Title = lp.Title,
        Slug = lp.Slug,
        Type = lp.Type.ToString(),
        Status = lp.Status.ToString(),
        CustomDomain = lp.CustomDomain,
        Sections = sections.OrderBy(s => s.SortOrder).Select(MapSectionToDto).ToList(),
        CreatedAt = lp.CreatedAt,
        UpdatedAt = lp.UpdatedAt
    };

    private static LandingPageSectionResponseDto MapSectionToDto(LandingPageSection s) => new()
    {
        PublicId = s.PublicId,
        Type = s.Type.ToString(),
        SortOrder = s.SortOrder,
        BackgroundColor = s.BackgroundColor,
        ContentJson = s.ContentJson,
        IsLocked = IsLockedSection(s.Type)
    };

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial System.Text.RegularExpressions.Regex SlugRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
    private static partial System.Text.RegularExpressions.Regex ColorRegex();
}
