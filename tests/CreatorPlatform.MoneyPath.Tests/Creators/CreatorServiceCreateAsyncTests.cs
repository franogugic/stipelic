using CreatorPlatform.Creators.Application;
using Microsoft.Extensions.Logging.Abstractions;
using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.MoneyPath.Tests.Creators;

public class CreatorServiceCreateAsyncTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreatorService BuildService() => BuildService(new FakeCreatorRepository());

    private static CreatorService BuildService(FakeCreatorRepository creatorRepository)
    {
        var planRepository = new FakeCreatorPlanRepository();
        planRepository.PlansByCode["free"] = CreatorPlan.Create(
            "free", "Free", null, 0, Currency.Eur, BillingInterval.None, 1000, null, Now);

        return new CreatorService(
            creatorRepository,
            new FakeCreatorMemberRepository(),
            planRepository,
            new FakeCreatorSettingsRepository(),
            new FakeCreatorSubscriptionRepository(),
            new FakeCreatorsUnitOfWork(),
            new FakeSubscriptionCheckoutSessionService(),
            new FakeSubscriptionCancellationService(),
            new FakeBillingPortalService(),
            new FakeBillingCustomerService(),
            new FakeCreatorCacheInvalidator(),
            NullLogger<CreatorService>.Instance, new FakeOwnedImageCheck());
    }

    private static CreateCreatorRequestDto BuildRequest(string countryCode) => new()
    {
        Name = "Acme Creator",
        Slug = "acme-creator",
        PlanCode = "free",
        DefaultCurrency = "EUR",
        CountryCode = countryCode,
    };

    [Theory]
    [InlineData("XX")]
    [InlineData("RS")]   // formerly a bank-transfer country
    [InlineData("BA")]
    [InlineData("AU")]   // a Stripe country our EEA platform cannot transfer to
    [InlineData("JP")]
    [InlineData("IS")]   // EEA, but not a Stripe account country
    public async Task CreateAsync_ACountryOutsideTheConnectList_Is400CountryNotSupported(string country)
    {
        var service = BuildService();

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreateAsync(1, BuildRequest(country), CancellationToken.None));

        Assert.Equal(CreatorErrorCodes.CountryNotSupported, exception.Code);
    }

    [Fact]
    public void TheConnectCountries_AreExactlyTheThirtyThreeOurPlatformCanTransferTo()
    {
        // EEA (without Iceland, not a Stripe account country) + GB, CH, US, CA — see PayoutCountries.
        Assert.Equal(
            ["AT", "BE", "BG", "CA", "CH", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GB", "GR", "HR", "HU", "IE", "IT",
             "LI", "LT", "LU", "LV", "MT", "NL", "NO", "PL", "PT", "RO", "SE", "SI", "SK", "US"],
            BuildService().GetPayoutCountries());
    }

    [Fact]
    public async Task CreateAsync_Croatia_IsAccepted()
    {
        var service = BuildService();

        var response = await service.CreateAsync(1, BuildRequest("HR"), CancellationToken.None);

        Assert.Equal("HR", response.Creator.CountryCode);
    }

    [Fact]
    public async Task CreateAsync_SlugTaken_ThrowsConflictWithSlugTakenCode()
    {
        var service = BuildService(new FakeCreatorRepository { SlugExists = true });

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(1, BuildRequest("HR"), CancellationToken.None));

        Assert.Equal("CREATOR_SLUG_TAKEN", exception.Code);
        Assert.Equal("This creator URL is already taken.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_OwnerAlreadyHasAWorkspace_ThrowsConflictWithAlreadyExistsCode()
    {
        var service = BuildService(new FakeCreatorRepository { ExistsByOwner = true });

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateAsync(1, BuildRequest("HR"), CancellationToken.None));

        Assert.Equal("CREATOR_ALREADY_EXISTS", exception.Code);
        Assert.Equal("You already have a creator workspace.", exception.Message);
    }

    [Fact]
    public void ConflictException_WithoutACode_KeepsTheGenericConflictCode()
    {
        Assert.Equal("CONFLICT", new ConflictException("Something conflicts.").Code);
    }
}
