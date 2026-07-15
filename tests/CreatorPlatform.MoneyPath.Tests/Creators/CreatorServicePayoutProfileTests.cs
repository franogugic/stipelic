using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.MoneyPath.Tests.Creators;

public class CreatorServicePayoutProfileTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static (CreatorService Service, FakeCreatorPayoutProfileRepository ProfileRepository) BuildService(Creator creator)
    {
        var creatorRepository = new FakeCreatorRepository { CreatorByOwner = creator };
        var profileRepository = new FakeCreatorPayoutProfileRepository();
        var planRepository = new FakeCreatorPlanRepository();

        var service = new CreatorService(
            creatorRepository,
            new FakeCreatorMemberRepository(),
            planRepository,
            new FakeCreatorSettingsRepository(),
            new FakeCreatorSubscriptionRepository(),
            profileRepository,
            new FakeCreatorsUnitOfWork(),
            new FakeSubscriptionCheckoutSessionService(),
            new FakeSubscriptionCancellationService(),
            new FakeBillingPortalService());

        return (service, profileRepository);
    }

    [Fact]
    public async Task UpdatePayoutProfileAsync_InvalidIbanChecksum_Throws()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "RS", PayoutMode.BankTransfer, Now);
        var (service, _) = BuildService(creator);

        var request = new UpdatePayoutProfileRequestDto
        {
            AccountHolderName = "Acme Doo",
            // Known-valid IBAN (GB29NWBK60161331926819) with its last digit flipped — same structure,
            // fails the mod-97 checksum.
            Iban = "GB29NWBK60161331926818",
            BankCountryCode = "GB"
        };

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.UpdatePayoutProfileAsync("acme", 1, request, CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePayoutProfileAsync_ValidIban_CreatesProfileWithMaskedIban()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "RS", PayoutMode.BankTransfer, Now);
        var (service, profileRepository) = BuildService(creator);

        var request = new UpdatePayoutProfileRequestDto
        {
            AccountHolderName = "Acme Doo",
            Iban = "GB29NWBK60161331926819",
            BankCountryCode = "GB"
        };

        var response = await service.UpdatePayoutProfileAsync("acme", 1, request, CancellationToken.None);

        Assert.Single(profileRepository.Added);
        Assert.StartsWith("GB29", response.MaskedIban);
        Assert.EndsWith("6819", response.MaskedIban);
        Assert.Contains('*', response.MaskedIban);
        Assert.DoesNotContain("NWBK60161331", response.MaskedIban);
    }
}
