using CreatorPlatform.Creators.Domain.Creators;
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
    // A section's content is a small JSON document (text, a few image URLs); the cap keeps one save from
    // storing megabytes in a jsonb column that every public page view reads.
    private const int ContentJsonMaxBytes = 64 * 1024;

    // Publish-block codes: the editor picks its explanation by code, never by message.
    public const string SubscriptionInactiveCode = "SUBSCRIPTION_INACTIVE";
    public const string PayoutsNotReadyCode = "PAYOUTS_NOT_READY";
    public const string PlanLimitReachedCode = "PLAN_LIMIT_REACHED";

    /// <summary>The <c>details</c> of a PLAN_LIMIT_REACHED conflict: "{used} of {limit} pages".</summary>
    public sealed record PlanLimitDetails(int Used, int Limit);

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

        var productId = await _creatorContextProvider.GetProductIdForCreatorAsync(creatorId, request.ProductId, ct);
        if (productId is null)
            throw new NotFoundException("Product not found.");

        var now = DateTimeOffset.UtcNow;
        var landingPage = LandingPage.Create(creatorId, productId, title, slug, type, now);

        await _landingPageRepository.AddAsync(landingPage, ct);

        // Every page starts with the four required sections, each in its type's default layout.
        LandingPageSectionType[] starterTypes =
            [LandingPageSectionType.Navbar, LandingPageSectionType.Hero, LandingPageSectionType.Cta, LandingPageSectionType.Footer];
        for (var i = 0; i < starterTypes.Length; i++)
        {
            var template = SectionTemplates.GetDefault(starterTypes[i]);
            await _sectionRepository.AddAsync(LandingPageSection.Create(
                landingPage, template.Type, template.Variant, i, template.DefaultBackgroundColor, template.ContentJson, now), ct);
        }
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(landingPage, await GetProductInfoAsync(landingPage, ct));
    }

    public async Task<List<LandingPageResponseDto>> ListAsync(
        string creatorSlug,
        int ownerUserId,
        bool includeArchived,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var pages = await _landingPageRepository.ListByCreatorIdAsync(creatorId, includeArchived, ct);

        var productIds = pages.Where(p => p.ProductId.HasValue).Select(p => p.ProductId!.Value).Distinct().ToList();
        var products = await _creatorContextProvider.GetProductInfosAsync(productIds, ct);

        return pages
            .Select(p => MapToDto(p, p.ProductId is int productId ? products.GetValueOrDefault(productId) : null))
            .ToList();
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

        return MapToWithSectionsDto(landingPage, sections, await GetProductInfoAsync(landingPage, ct));
    }

    public async Task<LandingPageResponseDto> GetSummaryAsync(
        string creatorSlug,
        Guid landingPagePublicId,
        int ownerUserId,
        CancellationToken ct)
    {
        var (creatorId, _, _) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var landingPage = await _landingPageRepository.GetByPublicIdAndCreatorIdAsync(landingPagePublicId, creatorId, ct);
        if (landingPage is null)
            throw new NotFoundException("Landing page not found.");

        return MapToDto(landingPage, await GetProductInfoAsync(landingPage, ct));
    }

    public async Task PublishAsync(string creatorSlug, Guid landingPagePublicId, int ownerUserId, CancellationToken ct)
    {
        var context = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var landingPage = await _landingPageRepository.GetByPublicIdAndCreatorIdForUpdateAsync(landingPagePublicId, context.CreatorId, ct);
        if (landingPage is null)
            throw new NotFoundException("Landing page not found.");

        if (landingPage.Status == LandingPageStatus.Archived)
            throw new BadRequestException("Archived landing pages cannot be published.");

        if (landingPage.Status == LandingPageStatus.Published)
            return;

        if (context.Status != CreatorStatus.Active)
            throw new ConflictException("Complete your subscription payment before publishing.", SubscriptionInactiveCode);

        // Creating and restoring already stop at the limit, and drafts count, so a workspace is only OVER it after
        // a downgrade. At the limit, publishing an existing draft is fine.
        if (context.MaxLandingPages >= 0 && context.ActiveLandingPageCount > context.MaxLandingPages)
            throw new ConflictException(
                $"Your plan includes {context.MaxLandingPages} landing page(s) and you have {context.ActiveLandingPageCount}. Archive a page or upgrade your plan to publish.",
                PlanLimitReachedCode,
                new PlanLimitDetails(context.ActiveLandingPageCount, context.MaxLandingPages));

        if (landingPage.Type == LandingPageType.Sales)
        {
            var payoutReady = context.PayoutMode == PayoutMode.StripeConnect
                ? context.StripeConnectPayoutsEnabled
                : context.HasPayoutProfile;

            if (!payoutReady)
            {
                var missing = context.PayoutMode == PayoutMode.StripeConnect
                    ? "Stripe Connect onboarding"
                    : "bank account details";
                throw new ConflictException($"Complete your payout setup ({missing}) before publishing a Sales page.", PayoutsNotReadyCode);
            }
        }

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

    public async Task<LandingPageResponseDto> RestoreAsync(
        string creatorSlug,
        Guid landingPagePublicId,
        int ownerUserId,
        CancellationToken ct)
    {
        var (creatorId, maxLandingPages, activeLandingPageCount) = await GetCreatorContextAsync(creatorSlug, ownerUserId, ct);

        var landingPage = await _landingPageRepository.GetByPublicIdAndCreatorIdForUpdateAsync(landingPagePublicId, creatorId, ct);
        if (landingPage is null)
            throw new NotFoundException("Landing page not found.");

        if (landingPage.Status != LandingPageStatus.Archived)
            throw new BadRequestException("Only archived landing pages can be restored.");

        if (maxLandingPages >= 0 && activeLandingPageCount >= maxLandingPages)
            throw new ConflictException(
                $"Your plan allows a maximum of {maxLandingPages} landing page(s). Free up {activeLandingPageCount - maxLandingPages + 1} landing page(s) before restoring.");

        if (await _landingPageRepository.SlugExistsForCreatorAsync(creatorId, landingPage.Slug, ct))
            throw new ConflictException("A landing page with this URL already exists. Update its URL slug before restoring, or archive the conflicting page.");

        landingPage.Restore(DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToDto(landingPage, await GetProductInfoAsync(landingPage, ct));
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
        if (request.Sections.Count < 4)
            throw new BadRequestException("Landing page must have at least Navbar, Hero, CTA and Footer sections.");

        var requestedTypes = request.Sections.Select(s => ParseSectionType(s.Type)).ToList();

        if (requestedTypes[0] != LandingPageSectionType.Navbar)
            throw new BadRequestException("First section must be Navbar.");

        if (requestedTypes[^1] != LandingPageSectionType.Footer)
            throw new BadRequestException("Last section must be Footer.");

        if (requestedTypes.Count(t => t == LandingPageSectionType.Navbar) != 1)
            throw new BadRequestException("Landing page must have exactly one Navbar section.");

        if (requestedTypes.Count(t => t == LandingPageSectionType.Footer) != 1)
            throw new BadRequestException("Landing page must have exactly one Footer section.");

        if (!requestedTypes.Contains(LandingPageSectionType.Hero))
            throw new BadRequestException("Landing page must have a Hero section.");

        if (!requestedTypes.Contains(LandingPageSectionType.Cta))
            throw new BadRequestException("Landing page must have a CTA section.");

        if (requestedTypes.Count(t => t == LandingPageSectionType.Hero) > 1)
            throw new BadRequestException("Landing page cannot have more than one Hero section.");

        if (requestedTypes.Count(t => t == LandingPageSectionType.Cta) > 1)
            throw new BadRequestException("Landing page cannot have more than one CTA section.");

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
                var variant = NormalizeVariant(sectionType, dto.Variant) ?? existing.Variant;
                existing.Update(variant, i, backgroundColor, contentJson, now);
                resultSections.Add(existing);
            }
            else
            {
                var variant = NormalizeVariant(sectionType, dto.Variant) ?? SectionTemplates.GetDefault(sectionType).Variant;
                var newSection = LandingPageSection.Create(landingPage, sectionType, variant, i, backgroundColor, contentJson, now);
                await _sectionRepository.AddAsync(newSection, ct);
                resultSections.Add(newSection);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToWithSectionsDto(landingPage, resultSections, await GetProductInfoAsync(landingPage, ct));
    }

    public List<SectionTemplateResponseDto> GetSectionTemplates()
    {
        // Navbar and Footer are included (flagged locked) so the editor can switch their layout.
        return SectionTemplates.All
            .Select(t => new SectionTemplateResponseDto
            {
                Key = t.Key,
                Type = t.Type.ToString(),
                Variant = t.Variant,
                Name = t.Name,
                Description = t.Description,
                ContentJson = t.ContentJson,
                DefaultBackgroundColor = t.DefaultBackgroundColor,
                IsLocked = IsLockedSection(t.Type)
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

    /// <summary>Null when no variant was sent (the caller keeps the current one or uses the default).</summary>
    private static string? NormalizeVariant(LandingPageSectionType type, string? value)
    {
        var variant = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(variant))
            return null;
        if (SectionTemplates.FindVariant(type, variant) is null)
        {
            var valid = SectionTemplates.All.Where(t => t.Type == type).Select(t => t.Variant);
            throw new BadRequestException($"Invalid layout for a {type} section. Valid values: {string.Join(", ", valid)}.");
        }
        return variant;
    }

    /// <summary>Null = the page default.</summary>
    private static string? NormalizeColor(string? value)
    {
        var color = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(color))
            return null;
        if (!ColorRegex().IsMatch(color))
            throw new BadRequestException("Background color must be a valid hex color (e.g. #ffffff).");
        return color.ToLowerInvariant();
    }

    private static string NormalizeContentJson(string? value)
    {
        var json = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(json))
            throw new BadRequestException("Section content is required.");
        if (System.Text.Encoding.UTF8.GetByteCount(json) > ContentJsonMaxBytes)
            throw new BadRequestException($"Section content is too large (max {ContentJsonMaxBytes / 1024} KB per section).");
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
        => type is LandingPageSectionType.Navbar or LandingPageSectionType.Footer;

    private async Task<ProductInfo?> GetProductInfoAsync(LandingPage landingPage, CancellationToken ct)
        => landingPage.ProductId is int productId
            ? await _creatorContextProvider.GetProductInfoAsync(productId, ct)
            : null;

    private static LandingPageResponseDto MapToDto(LandingPage lp, ProductInfo? product) => new()
    {
        Id = lp.Id,
        PublicId = lp.PublicId,
        Title = lp.Title,
        Slug = lp.Slug,
        Type = lp.Type.ToString(),
        Status = lp.Status.ToString(),
        ProductPublicId = product?.PublicId,
        ProductName = product?.Name,
        ProductThumbnailUrl = product?.ThumbnailUrl,
        CustomDomain = lp.CustomDomain,
        CreatedAt = lp.CreatedAt,
        UpdatedAt = lp.UpdatedAt
    };

    private static LandingPageWithSectionsResponseDto MapToWithSectionsDto(
        LandingPage lp, List<LandingPageSection> sections, ProductInfo? product) => new()
    {
        Id = lp.Id,
        ProductId = lp.ProductId,
        ProductPublicId = product?.PublicId,
        ProductName = product?.Name,
        ProductPriceCents = product?.PriceCents,
        ProductThumbnailUrl = product?.ThumbnailUrl,
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
        Variant = s.Variant,
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
