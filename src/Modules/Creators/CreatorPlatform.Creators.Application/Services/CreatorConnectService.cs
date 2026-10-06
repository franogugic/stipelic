using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Creators.Application.Services;

public sealed class CreatorConnectService : ICreatorConnectService
{
    public const string DashboardUnavailableCode = "connect_dashboard_unavailable";

    private readonly ICreatorRepository _creatorRepository;
    private readonly ICreatorsUnitOfWork _unitOfWork;
    private readonly IConnectAccountService _connectAccountService;
    private readonly IPayoutScheduleCache _payoutScheduleCache;
    private readonly StripeOptions _options;

    public CreatorConnectService(
        ICreatorRepository creatorRepository,
        ICreatorsUnitOfWork unitOfWork,
        IConnectAccountService connectAccountService,
        IPayoutScheduleCache payoutScheduleCache,
        IOptions<StripeOptions> options)
    {
        _creatorRepository = creatorRepository;
        _unitOfWork = unitOfWork;
        _connectAccountService = connectAccountService;
        _payoutScheduleCache = payoutScheduleCache;
        _options = options.Value;
    }

    public async Task<ConnectOnboardingLinkResponseDto> StartConnectOnboardingAsync(
        int ownerUserId, string ownerEmail, CancellationToken ct)
    {
        var creator = await _creatorRepository.GetByOwnerUserIdForUpdateAsync(ownerUserId, ct);
        if (creator is null)
            throw new NotFoundException("Creator workspace does not exist.");

        if (creator.PayoutMode != PayoutMode.StripeConnect)
            throw new BadRequestException("Connect onboarding is only available for Stripe-supported countries.");

        if (creator.StripeConnectAccountId is null)
        {
            var accountId = await _connectAccountService.CreateAccountAsync(creator.CountryCode, ownerEmail, ct);

            // Save the account id before generating the link — if the link call fails, we don't want to
            // lose track of the account and create a duplicate one on retry.
            creator.SetStripeConnectAccountId(accountId, DateTimeOffset.UtcNow);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var returnUrl = $"{_options.FrontendBaseUrl}/app/{creator.Slug}/settings?connect=return";
        var refreshUrl = $"{_options.FrontendBaseUrl}/app/{creator.Slug}/settings?connect=refresh";

        var url = await _connectAccountService.CreateOnboardingLinkAsync(
            creator.StripeConnectAccountId!, returnUrl, refreshUrl, ct);

        return new ConnectOnboardingLinkResponseDto(url);
    }

    public async Task<ConnectPayoutDetailsResponseDto> GetPayoutDetailsAsync(string slug, int ownerUserId, CancellationToken ct)
    {
        var creator = await _creatorRepository.GetBySlugForOwnerAsync(slug.Trim(), ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");

        var schedule = creator.StripeConnectAccountId is { } accountId
            ? await GetPayoutScheduleAsync(creator.Id, accountId, ct)
            : null;

        return new ConnectPayoutDetailsResponseDto(
            creator.StripeConnectAccountId,
            creator.StripeConnectDetailsSubmittedAt,
            creator.StripeConnectPayoutsEnabledAt,
            schedule);
    }

    public async Task<ConnectDashboardLinkResponseDto> CreateDashboardLoginLinkAsync(int ownerUserId, CancellationToken ct)
    {
        var creator = await _creatorRepository.GetByOwnerUserIdAsync(ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace does not exist.");

        // Stripe only issues login links for an Express account that has completed onboarding.
        if (creator.PayoutMode != PayoutMode.StripeConnect
            || creator.StripeConnectAccountId is not { } accountId
            || !creator.StripeConnectDetailsSubmitted)
        {
            throw new ConflictException(
                "The Stripe dashboard is available once your Stripe account is set up.", DashboardUnavailableCode);
        }

        return new ConnectDashboardLinkResponseDto(await _connectAccountService.CreateDashboardLoginLinkAsync(accountId, ct));
    }

    /// <summary>Only called after the ownership check above, so the cache is keyed by the creator's internal id.</summary>
    private async Task<PayoutScheduleDto?> GetPayoutScheduleAsync(int creatorId, string accountId, CancellationToken ct)
    {
        if (_payoutScheduleCache.TryGet(creatorId, out var cached) && cached is not null)
            return cached;

        var schedule = await _connectAccountService.GetPayoutScheduleAsync(accountId, ct);
        if (schedule is not null)
            _payoutScheduleCache.Set(creatorId, schedule);

        return schedule;
    }
}
