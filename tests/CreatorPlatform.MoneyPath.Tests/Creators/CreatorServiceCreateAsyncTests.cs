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

    private static CreatorService BuildService()
    {
        var planRepository = new FakeCreatorPlanRepository();
        planRepository.PlansByCode["free"] = CreatorPlan.Create(
            "free", "Free", null, 0, Currency.Eur, BillingInterval.None, 1000, null, Now);

        return new CreatorService(
            new FakeCreatorRepository(),
            new FakeCreatorMemberRepository(),
            planRepository,
            new FakeCreatorSettingsRepository(),
            new FakeCreatorSubscriptionRepository(),
            new FakeCreatorPayoutProfileRepository(),
            new FakeCreatorsUnitOfWork(),
            new FakeSubscriptionCheckoutSessionService(),
            new FakeSubscriptionCancellationService(),
            new FakeBillingPortalService());
    }

    private static CreateCreatorRequestDto BuildRequest(string countryCode) => new()
    {
        Name = "Acme Creator",
        Slug = "acme-creator",
        PlanCode = "free",
        DefaultCurrency = "EUR",
        CountryCode = countryCode,
    };

    [Fact]
    public async Task CreateAsync_UnsupportedCountry_Throws()
    {
        var service = BuildService();

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreateAsync(1, BuildRequest("XX"), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_Serbia_ResolvesToBankTransfer()
    {
        var service = BuildService();

        var response = await service.CreateAsync(1, BuildRequest("RS"), CancellationToken.None);

        Assert.Equal(nameof(PayoutMode.BankTransfer), response.Creator.PayoutMode);
    }

    [Fact]
    public async Task CreateAsync_Croatia_ResolvesToStripeConnect()
    {
        var service = BuildService();

        var response = await service.CreateAsync(1, BuildRequest("HR"), CancellationToken.None);

        Assert.Equal(nameof(PayoutMode.StripeConnect), response.Creator.PayoutMode);
    }
}
