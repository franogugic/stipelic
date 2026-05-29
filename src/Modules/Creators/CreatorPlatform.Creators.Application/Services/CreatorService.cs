using System.Text.RegularExpressions;
using System.Net.Mail;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.Creators.Application.Services;

public sealed partial class CreatorService : ICreatorService
{
    private const string FreePlanCode = "free";
    private const string DefaultPrimaryColor = "#111827";
    private const string DefaultTimezone = "Europe/Sarajevo";
    private const string DefaultLanguage = "en";
    private const string OnlySupportedLanguage = "en";
    private const int CreatorNameMaxLength = 50;
    private const int CreatorSlugMaxLength = 50;
    private const int BrandNameMaxLength = 50;
    private const int TimezoneMaxLength = 50;

    private readonly ICreatorRepository _creatorRepository;
    private readonly ICreatorMemberRepository _creatorMemberRepository;
    private readonly ICreatorPlanRepository _creatorPlanRepository;
    private readonly ICreatorSettingsRepository _creatorSettingsRepository;
    private readonly ICreatorSubscriptionRepository _creatorSubscriptionRepository;
    private readonly ICreatorsUnitOfWork _unitOfWork;

    public CreatorService(
        ICreatorRepository creatorRepository,
        ICreatorMemberRepository creatorMemberRepository,
        ICreatorPlanRepository creatorPlanRepository,
        ICreatorSettingsRepository creatorSettingsRepository,
        ICreatorSubscriptionRepository creatorSubscriptionRepository,
        ICreatorsUnitOfWork unitOfWork)
    {
        _creatorRepository = creatorRepository;
        _creatorMemberRepository = creatorMemberRepository;
        _creatorPlanRepository = creatorPlanRepository;
        _creatorSettingsRepository = creatorSettingsRepository;
        _creatorSubscriptionRepository = creatorSubscriptionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateCreatorResponseDto> CreateAsync(
        int ownerUserId,
        CreateCreatorRequestDto request,
        CancellationToken ct)
    {
        var name = NormalizeName(request.Name);
        var slug = NormalizeSlug(request.Slug, name);
        var planCode = NormalizePlanCode(request.PlanCode);
        var defaultCurrency = ParseCurrency(request.DefaultCurrency);
        var supportEmail = NormalizeOptionalEmail(request.SupportEmail);
        var brandName = NormalizeOptionalText(request.BrandName, BrandNameMaxLength) ?? name;
        var logoUrl = NormalizeOptionalUrl(request.LogoUrl);
        var primaryColor = NormalizePrimaryColor(request.PrimaryColor);
        var timezone = NormalizeTimezone(request.Timezone);
        var language = NormalizeLanguage(request.Language);

        if (await _creatorRepository.SlugExistsAsync(slug, ct))
            throw new ConflictException("This creator URL is already taken.");

        if (await _creatorRepository.ExistsByOwnerUserIdAsync(ownerUserId, ct))
            throw new ConflictException("You already have a creator workspace.");

        var plan = await _creatorPlanRepository.GetByCodeAsync(planCode, ct);
        if (plan is null || plan.Status != CreatorPlanStatus.Active)
            throw new BadRequestException("Selected creator plan is not available.");

        var requiresPayment = plan.Code != FreePlanCode;
        var creatorStatus = requiresPayment
            ? CreatorStatus.PendingPayment
            : CreatorStatus.Active;

        var createdAt = DateTimeOffset.UtcNow;
        var creator = Creator.Create(
            ownerUserId,
            name,
            slug,
            defaultCurrency,
            creatorStatus,
            createdAt);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var ownerMember = CreatorMember.CreateOwner(creator, ownerUserId, createdAt);
            var settings = CreatorSettings.Create(
                creator,
                supportEmail,
                brandName,
                logoUrl,
                primaryColor,
                timezone,
                language,
                createdAt);
            var subscription = requiresPayment
                ? CreatorSubscription.CreatePendingPayment(
                    creator,
                    plan,
                    plan.BillingInterval,
                    SubscriptionProvider.Internal,
                    null,
                    createdAt)
                : CreatorSubscription.CreateFree(creator, plan, createdAt);

            await _creatorRepository.AddAsync(creator, ct);
            await _creatorMemberRepository.AddAsync(ownerMember, ct);
            await _creatorSettingsRepository.AddAsync(settings, ct);
            await _creatorSubscriptionRepository.AddAsync(subscription, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return new CreateCreatorResponseDto
        {
            Creator = ToResponse(creator, plan),
            RequiresPayment = requiresPayment,
            PaymentStatus = requiresPayment
                ? CreatorSubscriptionStatus.PendingPayment.ToString()
                : CreatorSubscriptionStatus.Active.ToString(),
            CheckoutUrl = null
        };
    }

    public async Task<CreatorResponseDto?> GetCurrentForOwnerAsync(int ownerUserId, CancellationToken ct)
    {
        var creator = await _creatorRepository.GetByOwnerUserIdAsync(ownerUserId, ct);
        if (creator is null)
            return null;

        var subscription = await _creatorSubscriptionRepository.GetCurrentByCreatorIdAsync(creator.Id, ct);
        var planCode = subscription?.Plan.Code ?? string.Empty;

        return ToResponse(creator, planCode);
    }

    public async Task<CreatorSettingsResponseDto> GetSettingsAsync(
        string slug,
        int ownerUserId,
        CancellationToken ct)
    {
        var normalizedSlug = ValidateRouteSlug(slug);
        var settings = await _creatorSettingsRepository.GetByCreatorSlugForOwnerAsync(
            normalizedSlug,
            ownerUserId,
            ct);

        if (settings is null)
            throw new NotFoundException("Creator settings do not exist.");

        return ToSettingsResponse(settings);
    }

    public async Task<CreatorSettingsResponseDto> UpdateSettingsAsync(
        string slug,
        int ownerUserId,
        UpdateCreatorSettingsRequestDto request,
        CancellationToken ct)
    {
        var normalizedSlug = ValidateRouteSlug(slug);
        var settings = await _creatorSettingsRepository.GetForUpdateBySlugAndOwnerAsync(
            normalizedSlug,
            ownerUserId,
            ct);

        if (settings is null)
            throw new NotFoundException("Creator settings do not exist.");

        var update = CreatorSettingsUpdateValidator.Validate(request, settings.Creator.Name);
        var updatedAt = DateTimeOffset.UtcNow;

        settings.Update(
            update.SupportEmail,
            update.BrandName,
            update.LogoUrl,
            update.PrimaryColor,
            update.Timezone,
            update.Language,
            updatedAt);

        await _unitOfWork.SaveChangesAsync(ct);

        return ToSettingsResponse(settings);
    }

    public async Task DeleteCurrentAsync(int ownerUserId, CancellationToken ct)
    {
        var disabledAt = DateTimeOffset.UtcNow;
        var wasDisabled = await _creatorRepository.DisableByOwnerUserIdAsync(ownerUserId, disabledAt, ct);
        if (!wasDisabled)
            throw new NotFoundException("Creator workspace does not exist.");
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim();

        if (normalized.Length < 2)
            throw new BadRequestException("Creator name must be at least 2 characters.");

        if (normalized.Length > CreatorNameMaxLength)
            throw new BadRequestException($"Creator name cannot be longer than {CreatorNameMaxLength} characters.");

        return normalized;
    }

    private static string ValidateRouteSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new BadRequestException("Creator URL is required.");

        var normalized = slug.Trim();

        if (normalized.Length < 3)
            throw new BadRequestException("Creator URL must be at least 3 characters.");

        if (normalized.Length > CreatorSlugMaxLength)
            throw new BadRequestException($"Creator URL cannot be longer than {CreatorSlugMaxLength} characters.");

        if (!SlugRegex().IsMatch(normalized))
            throw new BadRequestException("Creator URL is not valid.");

        return normalized;
    }

    private static string NormalizeSlug(string slug, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(slug) ? fallback : slug;
        var normalized = source.Trim().ToLowerInvariant();
        normalized = InvalidSlugCharactersRegex().Replace(normalized, "-");
        normalized = DuplicateDashesRegex().Replace(normalized, "-").Trim('-');

        if (normalized.Length < 3)
            throw new BadRequestException("Creator URL must be at least 3 characters.");

        if (normalized.Length > CreatorSlugMaxLength)
            throw new BadRequestException($"Creator URL cannot be longer than {CreatorSlugMaxLength} characters.");

        return normalized;
    }

    private static string NormalizePlanCode(string planCode)
    {
        var normalized = planCode.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
            throw new BadRequestException("Creator plan is required.");

        return normalized;
    }

    private static Currency ParseCurrency(string currency)
    {
        return currency.Trim().ToUpperInvariant() switch
        {
            "EUR" => Currency.Eur,
            "USD" => Currency.Usd,
            _ => throw new BadRequestException("Currency is not supported.")
        };
    }

    private static string? NormalizeOptionalEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalized = email.Trim().ToLowerInvariant();

        if (normalized.Length > 100)
            throw new BadRequestException("Support email cannot be longer than 100 characters.");

        try
        {
            _ = new MailAddress(normalized);
        }
        catch (FormatException)
        {
            throw new BadRequestException("Support email is not valid.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new BadRequestException($"Value cannot be longer than {maxLength} characters.");

        return normalized;
    }

    private static string? NormalizeOptionalUrl(string? value)
    {
        var normalized = NormalizeOptionalText(value, 500);
        if (normalized is null)
            return null;

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BadRequestException("Logo URL must be a valid HTTP or HTTPS URL.");
        }

        return normalized;
    }

    private static string NormalizePrimaryColor(string? primaryColor)
    {
        if (string.IsNullOrWhiteSpace(primaryColor))
            return DefaultPrimaryColor;

        var normalized = primaryColor.Trim();

        if (!HexColorRegex().IsMatch(normalized))
            throw new BadRequestException("Primary color must be a valid hex color.");

        return normalized;
    }

    private static string NormalizeTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
            return DefaultTimezone;

        var normalized = timezone.Trim();

        if (normalized.Length > TimezoneMaxLength)
            throw new BadRequestException($"Timezone cannot be longer than {TimezoneMaxLength} characters.");

        if (!TimezoneRegex().IsMatch(normalized))
            throw new BadRequestException("Timezone is not valid.");

        return normalized;
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return DefaultLanguage;

        var normalized = language.Trim().ToLowerInvariant();

        if (normalized.Length > 10)
            throw new BadRequestException("Language cannot be longer than 10 characters.");

        if (!LanguageRegex().IsMatch(normalized))
            throw new BadRequestException("Language is not valid.");

        if (normalized != OnlySupportedLanguage)
            throw new BadRequestException("English is the only supported language right now.");

        return normalized;
    }

    private static CreatorResponseDto ToResponse(Creator creator, CreatorPlan plan)
    {
        return ToResponse(creator, plan.Code);
    }

    private static CreatorResponseDto ToResponse(Creator creator, string planCode)
    {
        return new CreatorResponseDto
        {
            PublicId = creator.PublicId,
            Name = creator.Name,
            Slug = creator.Slug,
            Status = creator.Status.ToString(),
            DefaultCurrency = creator.DefaultCurrency.ToString(),
            PlanCode = planCode
        };
    }

    private static CreatorSettingsResponseDto ToSettingsResponse(CreatorSettings settings)
    {
        return new CreatorSettingsResponseDto
        {
            CreatorPublicId = settings.Creator.PublicId,
            CreatorName = settings.Creator.Name,
            Slug = settings.Creator.Slug,
            DefaultCurrency = settings.Creator.DefaultCurrency.ToString(),
            SupportEmail = settings.SupportEmail ?? string.Empty,
            BrandName = settings.BrandName,
            LogoUrl = settings.LogoUrl ?? string.Empty,
            PrimaryColor = settings.PrimaryColor,
            Timezone = settings.Timezone,
            Language = settings.Language
        };
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex InvalidSlugCharactersRegex();

    [GeneratedRegex("-+")]
    private static partial Regex DuplicateDashesRegex();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorRegex();

    [GeneratedRegex("^[a-z]{2}(-[a-z]{2})?$")]
    private static partial Regex LanguageRegex();

    [GeneratedRegex("^[A-Za-z0-9_./+-]+$")]
    private static partial Regex TimezoneRegex();
}
