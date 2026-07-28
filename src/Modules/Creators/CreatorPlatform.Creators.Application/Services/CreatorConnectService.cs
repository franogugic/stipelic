using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Creators.Application.Services;

public sealed class CreatorConnectService : ICreatorConnectService
{
    private readonly ICreatorRepository _creatorRepository;
    private readonly ICreatorsUnitOfWork _unitOfWork;
    private readonly IConnectAccountService _connectAccountService;
    private readonly StripeOptions _options;

    public CreatorConnectService(
        ICreatorRepository creatorRepository,
        ICreatorsUnitOfWork unitOfWork,
        IConnectAccountService connectAccountService,
        IOptions<StripeOptions> options)
    {
        _creatorRepository = creatorRepository;
        _unitOfWork = unitOfWork;
        _connectAccountService = connectAccountService;
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
}
