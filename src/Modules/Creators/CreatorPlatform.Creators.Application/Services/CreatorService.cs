using System.Text.RegularExpressions;
using System.Net.Mail;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CreatorPlatform.Creators.Application.Services;

public sealed partial class CreatorService : ICreatorService
{
    private const string FreePlanCode = "free";
    private const string SlugTakenMessage = "This creator URL is already taken.";
    private const string AlreadyExistsMessage = "You already have a creator workspace.";
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
    private readonly ICreatorPayoutProfileRepository _creatorPayoutProfileRepository;
    private readonly ICreatorsUnitOfWork _unitOfWork;
    private readonly ISubscriptionCheckoutSessionService _subscriptionCheckoutSessionService;
    private readonly ISubscriptionCancellationService _subscriptionCancellationService;
    private readonly IBillingPortalService _billingPortalService;
    private readonly ICreatorOpenBalanceCheck _creatorOpenBalanceCheck;
    private readonly ILogger<CreatorService> _logger;

    public CreatorService(
        ICreatorRepository creatorRepository,
        ICreatorMemberRepository creatorMemberRepository,
        ICreatorPlanRepository creatorPlanRepository,
        ICreatorSettingsRepository creatorSettingsRepository,
        ICreatorSubscriptionRepository creatorSubscriptionRepository,
        ICreatorPayoutProfileRepository creatorPayoutProfileRepository,
        ICreatorsUnitOfWork unitOfWork,
        ISubscriptionCheckoutSessionService subscriptionCheckoutSessionService,
        ISubscriptionCancellationService subscriptionCancellationService,
        IBillingPortalService billingPortalService,
        ICreatorOpenBalanceCheck creatorOpenBalanceCheck,
        ILogger<CreatorService> logger)
    {
        _creatorRepository = creatorRepository;
        _creatorMemberRepository = creatorMemberRepository;
        _creatorPlanRepository = creatorPlanRepository;
        _creatorSettingsRepository = creatorSettingsRepository;
        _creatorSubscriptionRepository = creatorSubscriptionRepository;
        _creatorPayoutProfileRepository = creatorPayoutProfileRepository;
        _unitOfWork = unitOfWork;
        _subscriptionCheckoutSessionService = subscriptionCheckoutSessionService;
        _subscriptionCancellationService = subscriptionCancellationService;
        _billingPortalService = billingPortalService;
        _creatorOpenBalanceCheck = creatorOpenBalanceCheck;
        _logger = logger;
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
        var countryCode = NormalizeCountryCode(request.CountryCode);
        var supportEmail = NormalizeOptionalEmail(request.SupportEmail);
        var brandName = NormalizeOptionalText(request.BrandName, BrandNameMaxLength) ?? name;
        var logoUrl = NormalizeOptionalUrl(request.LogoUrl);
        var primaryColor = NormalizePrimaryColor(request.PrimaryColor);
        var timezone = NormalizeTimezone(request.Timezone);
        var language = NormalizeLanguage(request.Language);

        if (await _creatorRepository.SlugExistsAsync(slug, ct))
            throw new ConflictException(SlugTakenMessage, CreatorErrorCodes.SlugTaken);

        if (await _creatorRepository.ExistsByOwnerUserIdAsync(ownerUserId, ct))
            throw new ConflictException(AlreadyExistsMessage, CreatorErrorCodes.AlreadyExists);

        var plan = await GetAvailablePlanAsync(planCode, ct);

        var requiresPayment = plan.Code != FreePlanCode;
        var creatorStatus = requiresPayment
            ? CreatorStatus.PendingPayment
            : CreatorStatus.Active;

        var payoutMode = PayoutCountries.ResolveMode(countryCode);

        var createdAt = DateTimeOffset.UtcNow;
        var creator = Creator.Create(
            ownerUserId,
            name,
            slug,
            defaultCurrency,
            creatorStatus,
            countryCode,
            payoutMode,
            createdAt);

        CreatorSubscription? createdSubscription = null;

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
            createdSubscription = requiresPayment
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
            await _creatorSubscriptionRepository.AddAsync(createdSubscription, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        return new CreateCreatorResponseDto
        {
            // Freshly created — a payout profile can't exist yet.
            Creator = ToResponse(creator, createdSubscription, hasPayoutProfile: false),
            RequiresPayment = requiresPayment,
            PaymentStatus = requiresPayment
                ? CreatorSubscriptionStatus.PendingPayment.ToString()
                : CreatorSubscriptionStatus.Active.ToString()
        };
    }

    public async Task<CreatorResponseDto?> GetCurrentForOwnerAsync(int ownerUserId, CancellationToken ct)
    {
        var (creator, hasPayoutProfile) = await _creatorRepository.GetByOwnerUserIdWithPayoutProfileAsync(ownerUserId, ct);
        if (creator is null)
            return null;

        var subscription = await _creatorSubscriptionRepository.GetCurrentByCreatorIdAsync(creator.Id, ct);

        return ToResponse(creator, subscription, hasPayoutProfile);
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

    public async Task CancelSubscriptionAsync(int ownerUserId, CancellationToken ct)
    {
        var creator = await _creatorRepository.GetByOwnerUserIdAsync(ownerUserId, ct);
        if (creator is null)
            throw new NotFoundException("Creator workspace does not exist.");

        var subscription = await _creatorSubscriptionRepository.GetCurrentByCreatorIdAsync(creator.Id, ct);
        if (subscription is null)
            throw new NotFoundException("Creator subscription does not exist.");

        if (subscription.Status != CreatorSubscriptionStatus.Active)
            throw new BadRequestException("Only active subscriptions can be cancelled.");

        if (subscription.Provider != SubscriptionProvider.Stripe)
            throw new BadRequestException("Free plan subscriptions cannot be cancelled.");

        if (string.IsNullOrWhiteSpace(subscription.ProviderSubscriptionId))
            throw new BadRequestException("Subscription is not linked to a payment provider.");

        if (subscription.CancelAtPeriodEnd)
            throw new BadRequestException("Subscription is already scheduled for cancellation.");

        await _subscriptionCancellationService.CancelAtPeriodEndAsync(
            subscription.ProviderSubscriptionId, ct);

        var now = DateTimeOffset.UtcNow;
        var trackedSubscription = await _creatorSubscriptionRepository
            .GetByIdForUpdateAsync(subscription.Id, ct);

        trackedSubscription!.ScheduleCancel(now);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<string> GetBillingPortalUrlAsync(int ownerUserId, CancellationToken ct)
    {
        var creator = await _creatorRepository.GetByOwnerUserIdAsync(ownerUserId, ct);
        if (creator is null)
            throw new NotFoundException("Creator workspace does not exist.");

        if (string.IsNullOrWhiteSpace(creator.StripeCustomerId))
            throw new BadRequestException("No billing account found. Please contact support.");

        return await _billingPortalService.CreateSessionAsync(
            creator.StripeCustomerId,
            returnUrl: $"/app/{creator.Slug}",
            ct);
    }

    public async Task<int> DeleteCurrentAsync(int ownerUserId, CancellationToken ct)
    {
        var creator = await _creatorRepository.GetByOwnerUserIdAsync(ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace does not exist.");

        // 1. Money still owed to a bank-transfer creator blocks the deletion — checked before touching Stripe.
        if (creator.PayoutMode == PayoutMode.BankTransfer
            && await _creatorOpenBalanceCheck.HasOpenBalanceAsync(creator.Id, ct))
        {
            throw new ConflictException(
                "Request a payout of your remaining balance before deleting this workspace.",
                CreatorErrorCodes.WorkspaceHasBalance);
        }

        // 2–3. Stop the billing in Stripe first, outside the transaction: if Stripe fails, the call throws and the
        // workspace and its subscription stay exactly as they were.
        var subscription = await _creatorSubscriptionRepository.GetCurrentByCreatorIdAsync(creator.Id, ct);
        if (subscription is not null)
            await StopBillingAsync(creator, subscription, ct);

        // 4. Cancel the subscription locally and disable the workspace together.
        var now = DateTimeOffset.UtcNow;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            if (subscription is not null)
            {
                await _creatorSubscriptionRepository.LockForUpdateAsync(subscription.Id, ct);
                var trackedSubscription = await _creatorSubscriptionRepository.GetByIdForUpdateAsync(subscription.Id, ct);
                if (trackedSubscription is not null && trackedSubscription.Status != CreatorSubscriptionStatus.Cancelled)
                    trackedSubscription.Cancel(now);
            }

            var trackedCreator = await _creatorRepository.GetByIdForUpdateAsync(creator.Id, ct);
            // A concurrent deletion got here first.
            if (trackedCreator is null || trackedCreator.Status == CreatorStatus.Disabled)
                throw new NotFoundException("Creator workspace does not exist.");

            trackedCreator.Disable(now);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        _logger.LogInformation(
            "Creator workspace deleted (disabled). CreatorId: {CreatorId}, SubscriptionId: {SubscriptionId}",
            creator.Id, subscription?.Id);

        return creator.Id;
    }

    private async Task StopBillingAsync(Creator creator, CreatorSubscription subscription, CancellationToken ct)
    {
        // Only a Stripe-billed subscription is charged recurringly (a pending one is still Internal until paid).
        if (subscription.Provider == SubscriptionProvider.Stripe
            && subscription.Status is CreatorSubscriptionStatus.Active or CreatorSubscriptionStatus.PastDue)
        {
            if (string.IsNullOrWhiteSpace(subscription.ProviderSubscriptionId))
            {
                _logger.LogWarning(
                    "Deleting a workspace whose paid subscription has no Stripe subscription id; nothing to cancel in Stripe. CreatorId: {CreatorId}, SubscriptionId: {SubscriptionId}",
                    creator.Id, subscription.Id);
                return;
            }

            // Immediately, not at period end: a deleted workspace must not be charged again.
            await _subscriptionCancellationService.CancelImmediatelyAsync(subscription.ProviderSubscriptionId, ct);
            return;
        }

        if (subscription.Status == CreatorSubscriptionStatus.PendingPayment)
        {
            if (subscription.CheckoutSessionId is not { } checkoutSessionId)
            {
                _logger.LogWarning(
                    "Deleting a pending workspace without expiring a Checkout session: none is stored for its pending subscription. CreatorId: {CreatorId}, SubscriptionId: {SubscriptionId}",
                    creator.Id, subscription.Id);
                return;
            }

            var outcome = await _subscriptionCheckoutSessionService.ExpireAsync(checkoutSessionId, ct);
            if (outcome == CheckoutSessionExpireOutcome.AlreadyCompleted)
            {
                // The customer just paid; the checkout.session.completed webhook activates the plan.
                throw new ConflictException("Payment already completed.");
            }
        }
    }

    public async Task<PayoutProfileResponseDto?> GetPayoutProfileAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var normalizedSlug = ValidateRouteSlug(slug);
        var creator = await _creatorRepository.GetBySlugForOwnerAsync(normalizedSlug, ownerUserId, ct);
        if (creator is null)
            throw new NotFoundException("Creator workspace not found.");

        var profile = await _creatorPayoutProfileRepository.GetByCreatorIdAsync(creator.Id, ct);

        return profile is null ? null : ToPayoutProfileResponse(profile);
    }

    public async Task<PayoutProfileResponseDto> UpdatePayoutProfileAsync(
        string slug,
        int ownerUserId,
        UpdatePayoutProfileRequestDto request,
        CancellationToken ct)
    {
        var normalizedSlug = ValidateRouteSlug(slug);
        var creator = await _creatorRepository.GetForUpdateBySlugAndOwnerAsync(normalizedSlug, ownerUserId, ct);
        if (creator is null)
            throw new NotFoundException("Creator workspace not found.");

        var accountHolderName = NormalizeOptionalText(request.AccountHolderName, 100)
            ?? throw new BadRequestException("Account holder name is required.");
        var bankCountryCode = NormalizeBankCountryCode(request.BankCountryCode);

        var iban = IbanValidator.Normalize(request.Iban);
        if (!IbanValidator.IsValid(iban))
            throw new BadRequestException("IBAN is not valid.");

        var now = DateTimeOffset.UtcNow;
        var profile = await _creatorPayoutProfileRepository.GetForUpdateByCreatorIdAsync(creator.Id, ct);

        if (profile is null)
        {
            profile = CreatorPayoutProfile.Create(creator, accountHolderName, iban, bankCountryCode, now);
            await _creatorPayoutProfileRepository.AddAsync(profile, ct);
        }
        else
        {
            profile.Update(accountHolderName, iban, bankCountryCode, now);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return ToPayoutProfileResponse(profile);
    }

    public List<PayoutCountryDto> GetPayoutCountries()
    {
        var connect = PayoutCountries.ConnectCountries
            .Select(code => new PayoutCountryDto(code, PayoutMode.StripeConnect.ToString()));
        var bankTransfer = PayoutCountries.BankTransferCountries
            .Select(code => new PayoutCountryDto(code, PayoutMode.BankTransfer.ToString()));

        return connect.Concat(bankTransfer).OrderBy(c => c.Code, StringComparer.Ordinal).ToList();
    }

    public async Task<StartCreatorSubscriptionCheckoutResponseDto> StartSubscriptionCheckoutAsync(
        int ownerUserId,
        CancellationToken ct)
    {
        var creator = await _creatorRepository.GetByOwnerUserIdAsync(ownerUserId, ct);
        if (creator is null)
            throw new NotFoundException("Creator workspace does not exist.");

        if (creator.Status != CreatorStatus.PendingPayment)
            throw new BadRequestException("Creator workspace does not require payment.");

        var subscription = await _creatorSubscriptionRepository.GetCurrentByCreatorIdAsync(creator.Id, ct);
        if (subscription is null)
            throw new NotFoundException("Creator subscription does not exist.");

        if (subscription.Status != CreatorSubscriptionStatus.PendingPayment)
            throw new BadRequestException("Creator subscription does not require payment.");

        if (subscription.Plan.PriceCents <= 0 || subscription.Plan.BillingInterval == BillingInterval.None)
            throw new BadRequestException("Free creator plans do not require checkout.");

        if (string.IsNullOrWhiteSpace(subscription.Plan.StripePriceId))
            throw new BadRequestException("Stripe price is not configured for this creator plan.");

        var checkoutSession = await _subscriptionCheckoutSessionService.CreateAsync(
            subscription.Plan.StripePriceId,
            idempotencyKey: $"checkout-sub-{subscription.Id}-{subscription.Plan.StripePriceId}",
            new Dictionary<string, string>
            {
                ["creatorId"] = creator.Id.ToString(),
                ["creatorPublicId"] = creator.PublicId.ToString(),
                ["subscriptionId"] = subscription.Id.ToString(),
                ["planCode"] = subscription.Plan.Code,
            },
            ct);

        // Remember the session so it can be expired if the creator abandons the payment (see
        // ContinueOnFreePlanAsync). A re-checkout replaces it; Stripe's idempotency key returns the same
        // session for repeated calls within its window.
        var trackedSubscription = await _creatorSubscriptionRepository.GetByIdForUpdateAsync(subscription.Id, ct);
        trackedSubscription!.AttachCheckoutSession(checkoutSession.ProviderCheckoutSessionId, DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(ct);

        return new StartCreatorSubscriptionCheckoutResponseDto
        {
            RequiresPayment = true,
            PaymentStatus = subscription.Status.ToString(),
            CheckoutUrl = checkoutSession.CheckoutUrl
        };
    }

    public async Task<CreatorResponseDto> ContinueOnFreePlanAsync(int ownerUserId, CancellationToken ct)
    {
        var (creator, hasPayoutProfile) = await _creatorRepository.GetByOwnerUserIdWithPayoutProfileAsync(ownerUserId, ct);
        if (creator is null)
            throw new NotFoundException("Creator workspace does not exist.");

        var pendingSubscription = await _creatorSubscriptionRepository.GetCurrentByCreatorIdAsync(creator.Id, ct);
        EnsureWaitingForPayment(creator, pendingSubscription);

        var freePlan = await GetAvailablePlanAsync(FreePlanCode, ct);

        // Close the Checkout session first, outside the transaction: a Free workspace must never sit behind a
        // still-payable session. If Stripe can't be reached, ExpireAsync throws and nothing changes.
        if (pendingSubscription!.CheckoutSessionId is { } checkoutSessionId)
        {
            var outcome = await _subscriptionCheckoutSessionService.ExpireAsync(checkoutSessionId, ct);
            if (outcome == CheckoutSessionExpireOutcome.AlreadyCompleted)
            {
                // The customer paid; the checkout.session.completed webhook activates the paid plan.
                throw new ConflictException("Payment already completed.");
            }
        }
        else
        {
            _logger.LogWarning(
                "Switching a pending workspace to the Free plan without expiring a Checkout session: none is stored for its pending subscription (created before session ids were recorded). CreatorId: {CreatorId}, SubscriptionId: {SubscriptionId}",
                creator.Id, pendingSubscription.Id);
        }

        var now = DateTimeOffset.UtcNow;
        CreatorSubscription? freeSubscription = null;
        Creator? trackedCreator = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Serializes concurrent switches (e.g. a double click): the second request waits here, then sees the
            // subscription already Cancelled and gets the 409 below instead of creating a second Free plan.
            await _creatorSubscriptionRepository.LockForUpdateAsync(pendingSubscription.Id, ct);

            var subscription = await _creatorSubscriptionRepository.GetByIdForUpdateAsync(pendingSubscription.Id, ct);
            trackedCreator = await _creatorRepository.GetByIdForUpdateAsync(creator.Id, ct);
            EnsureWaitingForPayment(trackedCreator, subscription);

            subscription!.Cancel(now);
            // The same Free subscription a free sign-up gets in CreateAsync.
            freeSubscription = CreatorSubscription.CreateFree(trackedCreator!, freePlan, now);
            trackedCreator!.Activate(now);

            await _creatorSubscriptionRepository.AddAsync(freeSubscription, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        _logger.LogInformation(
            "Pending workspace switched to the Free plan. CreatorId: {CreatorId}, CancelledSubscriptionId: {SubscriptionId}",
            creator.Id, pendingSubscription.Id);

        return ToResponse(trackedCreator!, freeSubscription, hasPayoutProfile);
    }

    private static void EnsureWaitingForPayment(Creator? creator, CreatorSubscription? subscription)
    {
        if (creator?.Status != CreatorStatus.PendingPayment
            || subscription?.Status != CreatorSubscriptionStatus.PendingPayment)
        {
            throw new ConflictException("Only a workspace that is waiting for its first payment can switch to the Free plan.");
        }
    }

    private async Task<CreatorPlan> GetAvailablePlanAsync(string planCode, CancellationToken ct)
    {
        var plan = await _creatorPlanRepository.GetByCodeAsync(planCode, ct);
        if (plan is null || plan.Status != CreatorPlanStatus.Active)
            throw new BadRequestException("Selected creator plan is not available.");

        return plan;
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

    private static string NormalizeCountryCode(string countryCode)
    {
        var normalized = countryCode.Trim().ToUpperInvariant();

        if (!PayoutCountries.IsSupported(normalized))
            throw new BadRequestException("Country is not supported yet.");

        return normalized;
    }

    private static string NormalizeBankCountryCode(string bankCountryCode)
    {
        var normalized = bankCountryCode.Trim().ToUpperInvariant();

        if (!CountryCodeRegex().IsMatch(normalized))
            throw new BadRequestException("Bank country code must be a 2-letter ISO code.");

        return normalized;
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

    private static CreatorResponseDto ToResponse(Creator creator, CreatorSubscription? subscription, bool hasPayoutProfile)
    {
        var payoutReady = creator.PayoutMode == PayoutMode.StripeConnect
            ? creator.StripeConnectPayoutsEnabled
            : hasPayoutProfile;

        return new CreatorResponseDto
        {
            PublicId = creator.PublicId,
            Name = creator.Name,
            Slug = creator.Slug,
            Status = creator.Status.ToString(),
            DefaultCurrency = creator.DefaultCurrency.ToString(),
            PlanCode = subscription?.Plan.Code ?? string.Empty,
            PlanName = subscription?.Plan.Name ?? string.Empty,
            CancelAtPeriodEnd = subscription?.CancelAtPeriodEnd ?? false,
            CurrentPeriodEnd = subscription?.CurrentPeriodEnd,
            CountryCode = creator.CountryCode,
            PayoutMode = creator.PayoutMode.ToString(),
            StripeConnectDetailsSubmitted = creator.StripeConnectDetailsSubmitted,
            StripeConnectPayoutsEnabled = creator.StripeConnectPayoutsEnabled,
            HasPayoutProfile = hasPayoutProfile,
            PayoutReady = payoutReady,
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

    private static PayoutProfileResponseDto ToPayoutProfileResponse(CreatorPayoutProfile profile)
    {
        return new PayoutProfileResponseDto
        {
            AccountHolderName = profile.AccountHolderName,
            MaskedIban = MaskIban(profile.Iban),
            BankCountryCode = profile.BankCountryCode
        };
    }

    // "HR12 **** **** 3456" style — first 4 + last 4 characters visible, everything between masked in
    // groups of 4. The full IBAN is never returned by the API once saved.
    private static string MaskIban(string iban)
    {
        const int visiblePrefixLength = 4;
        const int visibleSuffixLength = 4;

        if (iban.Length <= visiblePrefixLength + visibleSuffixLength)
            return iban;

        var prefix = iban[..visiblePrefixLength];
        var suffix = iban[^visibleSuffixLength..];
        var maskedLength = iban.Length - visiblePrefixLength - visibleSuffixLength;

        var groups = new List<string> { prefix };
        for (var i = 0; i < maskedLength; i += 4)
            groups.Add(new string('*', Math.Min(4, maskedLength - i)));
        groups.Add(suffix);

        return string.Join(' ', groups);
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

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryCodeRegex();
}
