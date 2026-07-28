using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Orders;

public class OrderCheckoutServiceTests
{
    private const string CreatorSlug = "acme";
    private const string LandingPageSlug = "sale-page";
    private const string Email = "buyer@example.com";

    private static LandingPageProductInfo BuildProductInfo(
        CreatorStatus creatorStatus = CreatorStatus.Active,
        PayoutMode payoutMode = PayoutMode.StripeConnect,
        string? stripeConnectAccountId = "acct_123",
        bool stripeConnectPayoutsEnabled = true,
        bool hasPayoutProfile = true,
        int priceCents = 1000,
        int? platformFeeBasisPoints = 500,
        string? thumbnailUrl = null) => new(
        CreatorId: 1,
        ProductId: 2,
        LandingPageId: 3,
        ProductName: "Course",
        ThumbnailUrl: thumbnailUrl,
        PriceCents: priceCents,
        Currency: Currency.Eur,
        CreatorStatus: creatorStatus,
        PayoutMode: payoutMode,
        StripeConnectAccountId: stripeConnectAccountId,
        StripeConnectPayoutsEnabled: stripeConnectPayoutsEnabled,
        HasPayoutProfile: hasPayoutProfile,
        PlatformFeeBasisPoints: platformFeeBasisPoints);

    private static (OrderCheckoutService Service, FakeOrdersCreatorContextProvider ContextProvider,
        FakePaymentCheckoutSessionService CheckoutSessionService, FakeOrderRepository OrderRepository) BuildService(
        LandingPageProductInfo? productInfo)
    {
        var contextProvider = new FakeOrdersCreatorContextProvider { ProductInfo = productInfo };
        var checkoutSessionService = new FakePaymentCheckoutSessionService();
        var orderRepository = new FakeOrderRepository();
        var unitOfWork = new FakeOrdersUnitOfWork();
        var options = Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.example.com" });

        var service = new OrderCheckoutService(
            contextProvider,
            checkoutSessionService,
            orderRepository,
            unitOfWork,
            options,
            NullLogger<OrderCheckoutService>.Instance);

        return (service, contextProvider, checkoutSessionService, orderRepository);
    }

    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(500, 1000, 50)]
    [InlineData(10_000, 1000, 1000)]
    public async Task CreateCheckoutAsync_SnapshotsFeeFromDto(int feeBasisPoints, int priceCents, int expectedFeeCents)
    {
        var productInfo = BuildProductInfo(priceCents: priceCents, platformFeeBasisPoints: feeBasisPoints);
        var (service, _, _, orderRepository) = BuildService(productInfo);

        await service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None);

        Assert.NotNull(orderRepository.AddedOrder);
        Assert.Equal(expectedFeeCents, orderRepository.AddedOrder!.PlatformFeeCents);
        Assert.Equal(feeBasisPoints, orderRepository.AddedOrder.PlatformFeeBasisPoints);
    }

    [Fact]
    public async Task CreateCheckoutAsync_StripeConnect_CallsSessionWithFeeAndDestination()
    {
        var productInfo = BuildProductInfo(payoutMode: PayoutMode.StripeConnect, stripeConnectAccountId: "acct_999");
        var (service, _, checkoutSessionService, orderRepository) = BuildService(productInfo);

        await service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None);

        Assert.Equal(1, checkoutSessionService.CallCount);
        Assert.Equal(50, checkoutSessionService.LastApplicationFeeAmountCents);
        Assert.Equal("acct_999", checkoutSessionService.LastDestinationAccountId);
        Assert.Equal(nameof(PayoutMode.StripeConnect), orderRepository.AddedOrder!.PayoutMode);
    }

    [Fact]
    public async Task CreateCheckoutAsync_BankTransfer_CallsSessionWithoutFeeOrDestination()
    {
        var productInfo = BuildProductInfo(payoutMode: PayoutMode.BankTransfer, hasPayoutProfile: true);
        var (service, _, checkoutSessionService, orderRepository) = BuildService(productInfo);

        await service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None);

        Assert.Equal(1, checkoutSessionService.CallCount);
        Assert.Null(checkoutSessionService.LastApplicationFeeAmountCents);
        Assert.Null(checkoutSessionService.LastDestinationAccountId);
        Assert.Equal(nameof(PayoutMode.BankTransfer), orderRepository.AddedOrder!.PayoutMode);
    }

    [Fact]
    public async Task CreateCheckoutAsync_StripeConnectNotPayoutsEnabled_Throws()
    {
        var productInfo = BuildProductInfo(payoutMode: PayoutMode.StripeConnect, stripeConnectPayoutsEnabled: false);
        var (service, _, _, _) = BuildService(productInfo);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None));
    }

    [Fact]
    public async Task CreateCheckoutAsync_BankTransferNoPayoutProfile_Throws()
    {
        var productInfo = BuildProductInfo(payoutMode: PayoutMode.BankTransfer, hasPayoutProfile: false);
        var (service, _, _, _) = BuildService(productInfo);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None));
    }

    [Fact]
    public async Task CreateCheckoutAsync_CreatorNotActive_Throws()
    {
        var productInfo = BuildProductInfo(creatorStatus: CreatorStatus.Suspended);
        var (service, _, _, _) = BuildService(productInfo);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None));
    }

    [Fact]
    public async Task CreateCheckoutAsync_NoActiveSubscription_Throws()
    {
        var productInfo = BuildProductInfo(platformFeeBasisPoints: null);
        var (service, _, _, _) = BuildService(productInfo);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None));
    }

    [Fact]
    public async Task CreateCheckoutAsync_ZeroPrice_Throws()
    {
        var productInfo = BuildProductInfo(priceCents: 0);
        var (service, _, _, _) = BuildService(productInfo);

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None));
    }

    [Fact]
    public async Task CreateCheckoutAsync_WithThumbnail_PassesThumbnailUrlToSession()
    {
        var productInfo = BuildProductInfo(thumbnailUrl: "https://cdn.example.com/thumb.jpg");
        var (service, _, checkoutSessionService, _) = BuildService(productInfo);

        await service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None);

        Assert.Equal("https://cdn.example.com/thumb.jpg", checkoutSessionService.LastThumbnailUrl);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WithoutThumbnail_PassesNullThumbnailUrlToSession()
    {
        var productInfo = BuildProductInfo(thumbnailUrl: null);
        var (service, _, checkoutSessionService, _) = BuildService(productInfo);

        await service.CreateCheckoutAsync(CreatorSlug, LandingPageSlug, Email, CancellationToken.None);

        Assert.Null(checkoutSessionService.LastThumbnailUrl);
    }
}
