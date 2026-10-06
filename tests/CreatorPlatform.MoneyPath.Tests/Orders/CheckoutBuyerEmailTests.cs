using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Shared.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Orders;

/// <summary>The buy button goes straight to Stripe: Checkout collects the buyer's email, and the order gets it
/// from checkout.session.completed. An email given up front is prefilled and kept.</summary>
public class CheckoutBuyerEmailTests
{
    private static readonly LandingPageProductInfo Product = new(
        CreatorId: 1, ProductId: 2, LandingPageId: 3, ProductName: "Course", ThumbnailUrl: null, PriceCents: 2900,
        Currency: Currency.Eur, CreatorStatus: CreatorStatus.Active, PayoutMode: PayoutMode.BankTransfer,
        StripeConnectAccountId: null, StripeConnectPayoutsEnabled: false, HasPayoutProfile: true, PlatformFeeBasisPoints: 500);

    private readonly FakePaymentCheckoutSessionService _stripe = new();
    private readonly FakeOrderRepository _orders = new();
    private readonly FakeLedgerEntryRepository _ledger = new();
    private readonly FakeEmailOutboxService _outbox = new();

    /// <summary>Checkout through the real service; returns the Pending order it created.</summary>
    private async Task<Order> CheckoutAsync(string? email)
    {
        var service = new OrderCheckoutService(
            new FakeOrdersCreatorContextProvider { ProductInfo = Product },
            _stripe,
            _orders,
            new FakeOrdersUnitOfWork(),
            Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.example.com" }),
            NullLogger<OrderCheckoutService>.Instance);

        await service.CreateCheckoutAsync("acme", "sale-page", email, CancellationToken.None);
        return _orders.AddedOrder!;
    }

    private Task CompleteAsync(Order order, string? stripeEmail, string? stripeName = null)
    {
        var service = new OrderWebhookService(
            new FakeWebhookOrderRepository { Order = order },
            new FakeOrdersUnitOfWork(),
            new FakeOrdersCreatorContextProvider(),
            _outbox,
            new FakeHomeSummaryCache(),
            new PayoutLedgerService(_ledger, NullLogger<PayoutLedgerService>.Instance),
            Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.example.com", ApiBaseUrl = "https://api.example.com" }),
            NullLogger<OrderWebhookService>.Instance);

        return service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId, "pi_1", stripeEmail, stripeName), CancellationToken.None);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WithoutAnEmail_TheOrderGetsTheOneTypedIntoStripe(string? email)
    {
        var order = await CheckoutAsync(email);
        Assert.Null(_stripe.LastCustomerEmail);
        Assert.Null(order.Email);

        await CompleteAsync(order, "ana@example.com", "Ana Horvat");

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(("ana@example.com", "Ana Horvat"), (order.Email, order.Name));
        // Everything after the webhook reads the completed email: the access email goes there and names it.
        Assert.Equal("ana@example.com", _outbox.LastOrderAccessTo);
        Assert.Equal("ana@example.com", _outbox.LastOrderAccess!.BuyerEmail);
        Assert.Equal("Ana Horvat", _outbox.LastOrderAccess.BuyerName);
        Assert.Equal(2, _ledger.Entries.Count);
    }

    [Fact]
    public async Task WithAnEmail_ItIsPrefilledInStripe_AndKept()
    {
        var order = await CheckoutAsync("  marko@example.com ");
        Assert.Equal("marko@example.com", _stripe.LastCustomerEmail);
        Assert.Equal("marko@example.com", order.Email);

        await CompleteAsync(order, "someone-else@example.com");

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal("marko@example.com", order.Email);
        Assert.Equal("marko@example.com", _outbox.LastOrderAccessTo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task NoEmailFromStripe_FailsTheWebhook_AndTheOrderStaysPending(string? stripeEmail)
    {
        var order = await CheckoutAsync(null);

        // The controller records any handler exception as a webhook failure for reprocessing.
        await Assert.ThrowsAsync<InvalidOperationException>(() => CompleteAsync(order, stripeEmail));

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.Email);
        Assert.Empty(_ledger.Entries);
        Assert.Equal(0, _outbox.OrderAccessQueuedCount);
    }

    [Fact]
    public async Task AVeryLongStripeName_IsCutToTheColumn()
    {
        var order = await CheckoutAsync(null);

        await CompleteAsync(order, "ana@example.com", new string('A', 80));

        Assert.Equal(Order.NameMaxLength, order.Name!.Length);
    }
}
