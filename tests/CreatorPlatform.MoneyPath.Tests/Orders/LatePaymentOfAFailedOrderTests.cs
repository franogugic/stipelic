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

/// <summary>The maintenance job fails checkouts still Pending after 24 h. If Stripe still delivers the payment afterwards
/// (a delayed webhook retry), the sale must count exactly as if it had arrived in time: paid, access link, email.</summary>
public class LatePaymentOfAFailedOrderTests
{
    private static Order FailedOrder()
    {
        var order = Order.Create(1, 2, 3, "buyer@example.com", null, 1000, Currency.Eur, 500, 50, "sess_late", DateTimeOffset.UtcNow.AddDays(-2));
        order.MarkFailed(DateTimeOffset.UtcNow.AddDays(-1));
        return order;
    }

    [Fact]
    public async Task ALatePayment_PaysAnOrderTheJobFailed_WithEverythingAPaidOrderGets()
    {
        var order = FailedOrder();
        var orders = new FakeWebhookOrderRepository { Order = order };
        var grants = new FakeOrderAccessGrants();
        var outbox = new FakeEmailOutboxService();
        var service = new OrderWebhookService(
            orders,
            new FakeOrdersUnitOfWork(),
            new FakeOrdersCreatorContextProvider(),
            outbox,
            new FakeHomeSummaryCache(),
            grants,
            Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.example.com" }),
            NullLogger<OrderWebhookService>.Instance);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto("sess_late", "pi_late"), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, orders.Order!.Status);
        Assert.Equal("pi_late", orders.Order.StripePaymentIntentId);
        Assert.Single(grants.ByOrderId);
        Assert.Equal(1, outbox.OrderAccessQueuedCount);
    }
}
