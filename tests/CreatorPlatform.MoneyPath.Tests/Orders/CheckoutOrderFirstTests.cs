using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Orders;

/// <summary>The Pending order is saved before its Stripe Checkout session exists, so a payable session never lacks its
/// order: the session carries the order's PublicId (also the idempotency key); a failed Stripe call fails the order; a
/// failed save of the session id still hands out the checkout URL (the webhook links it from the metadata).</summary>
public class CheckoutOrderFirstTests
{
    private sealed record Harness(
        OrderCheckoutService Service,
        FakePaymentCheckoutSessionService Sessions,
        FakeOrderRepository Orders,
        FakeOrdersUnitOfWork UnitOfWork);

    private static Harness Build(PayoutMode payoutMode = PayoutMode.BankTransfer)
    {
        var context = new FakeOrdersCreatorContextProvider
        {
            ProductInfo = new LandingPageProductInfo(
                CreatorId: 1, ProductId: 2, LandingPageId: 3, ProductName: "Course", ThumbnailUrl: null, PriceCents: 5000,
                Currency: Currency.Eur, CreatorStatus: CreatorStatus.Active, PayoutMode: payoutMode,
                StripeConnectAccountId: "acct_1", StripeConnectPayoutsEnabled: true, HasPayoutProfile: true,
                PlatformFeeBasisPoints: 500)
        };
        var sessions = new FakePaymentCheckoutSessionService();
        var orders = new FakeOrderRepository();
        var unitOfWork = new FakeOrdersUnitOfWork();
        var service = new OrderCheckoutService(
            context, sessions, orders, unitOfWork,
            Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.example.com" }),
            NullLogger<OrderCheckoutService>.Instance);
        return new Harness(service, sessions, orders, unitOfWork);
    }

    [Theory]
    [InlineData(PayoutMode.BankTransfer)]
    [InlineData(PayoutMode.StripeConnect)]
    public async Task ThePendingOrder_IsSavedBeforeStripeIsCalled_AndTheSessionCarriesItsPublicId(PayoutMode payoutMode)
    {
        var harness = Build(payoutMode);
        (int Saves, OrderStatus? Status, string? SessionId)? atStripeCall = null;
        harness.Sessions.OnCreate = () =>
            atStripeCall = (harness.UnitOfWork.SaveCount, harness.Orders.AddedOrder?.Status, harness.Orders.AddedOrder?.StripeCheckoutSessionId);

        var result = await harness.Service.CreateCheckoutAsync("acme", "sale", "buyer@example.com", CancellationToken.None);

        Assert.Equal((1, OrderStatus.Pending, null), atStripeCall);
        var order = harness.Orders.AddedOrder!;
        Assert.Equal(order.PublicId.ToString(), harness.Sessions.LastMetadata![CheckoutSessionMetadata.OrderId]);
        Assert.Equal("2", harness.Sessions.LastMetadata[CheckoutSessionMetadata.ProductId]);
        Assert.Equal(order.PublicId.ToString(), harness.Sessions.LastIdempotencyKey);
        Assert.Equal("sess_123", order.StripeCheckoutSessionId);
        Assert.Equal(2, harness.UnitOfWork.SaveCount);
        Assert.Equal("https://checkout.stripe.com/sess_123", result.CheckoutUrl);
    }

    [Fact]
    public async Task AFailedStripeCall_FailsTheOrder_AndPropagates()
    {
        var harness = Build();
        harness.Sessions.Failure = new HttpRequestException("Stripe unreachable");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            harness.Service.CreateCheckoutAsync("acme", "sale", "buyer@example.com", CancellationToken.None));

        var order = harness.Orders.AddedOrder!;
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Null(order.StripeCheckoutSessionId);
        Assert.Equal(2, harness.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task AFailedSaveOfTheSessionId_StillReturnsTheCheckoutUrl()
    {
        var harness = Build();
        harness.UnitOfWork.FailOnSaveNumber = 2;

        var result = await harness.Service.CreateCheckoutAsync("acme", "sale", "buyer@example.com", CancellationToken.None);

        Assert.Equal("https://checkout.stripe.com/sess_123", result.CheckoutUrl);
        Assert.Equal(OrderStatus.Pending, harness.Orders.AddedOrder!.Status);
    }

    [Fact]
    public async Task AFailedSaveOfTheOrder_NeverCallsStripe()
    {
        var harness = Build();
        harness.UnitOfWork.FailOnSaveNumber = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Service.CreateCheckoutAsync("acme", "sale", "buyer@example.com", CancellationToken.None));

        Assert.Equal(0, harness.Sessions.CallCount);
    }
}
