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

public class OrderWebhookServiceTests
{
    private static Order BuildOrder(PayoutMode payoutMode, int amountCents = 1000, int platformFeeCents = 50, string sessionId = "sess_1")
        => Order.Create(
            creatorId: 1,
            productId: 2,
            landingPageId: 3,
            email: "buyer@example.com",
            name: null,
            amountCents,
            Currency.Eur,
            platformFeeBasisPoints: 500,
            platformFeeCents,
            payoutMode: payoutMode.ToString(),
            stripeCheckoutSessionId: sessionId,
            DateTimeOffset.UtcNow);

    private static (OrderWebhookService Service, FakeWebhookOrderRepository OrderRepository) BuildService(Order order)
    {
        var (service, orderRepository, _, _) = BuildServiceWithDelivery(order);
        return (service, orderRepository);
    }

    private static (OrderWebhookService Service, FakeWebhookOrderRepository OrderRepository, FakeOrderAccessGrants Grants,
        FakeEmailOutboxService Outbox)
        BuildServiceWithDelivery(Order order)
    {
        var orderRepository = new FakeWebhookOrderRepository { Order = order };
        var contextProvider = new FakeOrdersCreatorContextProvider();
        var unitOfWork = new FakeOrdersUnitOfWork();
        var emailOutbox = new FakeEmailOutboxService();
        var homeSummaryCache = new FakeHomeSummaryCache();
        var options = Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.example.com" });
        var grants = new FakeOrderAccessGrants();

        var service = new OrderWebhookService(
            orderRepository,
            unitOfWork,
            contextProvider,
            emailOutbox,
            homeSummaryCache,
            grants,
            options,
            NullLogger<OrderWebhookService>.Instance);

        return (service, orderRepository, grants, emailOutbox);
    }

    /// <summary>charge.refunded for the default 1000-cent order, charged in the order's own currency.</summary>
    private static OrderChargeRefundedDto FullRefund() => new("pi_1", "ch_1", 1000, 1000, FullyRefunded: true);

    private static OrderChargeRefundedDto PartialRefund(long refunded, long chargeAmount = 1000) =>
        new("pi_1", "ch_1", chargeAmount, refunded, FullyRefunded: false);

    [Fact]
    public async Task HandleCheckoutSessionCompleted_PaysTheOrder()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        var (service, orderRepository) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, orderRepository.Order!.Status);
    }

    [Fact]
    public async Task HandleCheckoutSessionCompleted_CalledTwice_RemainsIdempotent()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        var (service, _) = BuildService(order);
        var data = new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1");

        await service.HandleCheckoutSessionCompletedAsync(data, CancellationToken.None);
        var paidAt = order.PaidAt;
        await service.HandleCheckoutSessionCompletedAsync(data, CancellationToken.None);

        Assert.Equal((OrderStatus.Paid, paidAt), (order.Status, order.PaidAt));
    }

    [Fact]
    public async Task HandleChargeRefunded_InFull_RefundsTheWholeOrder()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow, buyerEmail: null, buyerName: null);
        var (service, orderRepository) = BuildService(order);

        await service.HandleChargeRefundedAsync(FullRefund(), CancellationToken.None);

        Assert.Equal((OrderStatus.Refunded, 1000), (orderRepository.Order!.Status, orderRepository.Order.RefundedCents));
    }

    [Fact]
    public async Task HandleChargeRefunded_CalledTwice_RemainsIdempotent()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow, buyerEmail: null, buyerName: null);
        var (service, _) = BuildService(order);
        var data = FullRefund();

        await service.HandleChargeRefundedAsync(data, CancellationToken.None);
        var refundedAt = order.UpdatedAt;
        await service.HandleChargeRefundedAsync(data, CancellationToken.None);

        Assert.Equal((OrderStatus.Refunded, 1000, refundedAt), (order.Status, order.RefundedCents, order.UpdatedAt));
    }

    // ---- F8: the buyer's personal link ---------------------------------------------------------------------------

    [Fact]
    public async Task APaidOrder_GetsExactlyOneGrant_AndItsEmailLinksTheDownloadPage()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        var (service, _, grants, outbox) = BuildServiceWithDelivery(order);
        var completed = new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1");

        await service.HandleCheckoutSessionCompletedAsync(completed, CancellationToken.None);
        await service.HandleCheckoutSessionCompletedAsync(completed, CancellationToken.None);

        var grant = Assert.Single(grants.ByOrderId).Value;
        Assert.Equal("buyer@example.com", grant.Email);
        Assert.Null(grant.RevokedAt);
        Assert.Equal(1, outbox.OrderAccessQueuedCount);
        Assert.Equal($"https://app.example.com/d/{grant.Token}", outbox.LastOrderAccess!.DownloadUrl);
        Assert.Equal("ZIP · 17.5 MB", outbox.LastOrderAccess.FileLabel);
    }

    [Fact]
    public async Task ARefund_TurnsTheBuyersLinkOff_Once()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        var (service, _, grants, _) = BuildServiceWithDelivery(order);
        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);

        var refund = FullRefund();
        await service.HandleChargeRefundedAsync(refund, CancellationToken.None);
        var revokedAt = Assert.Single(grants.ByOrderId).Value.RevokedAt;
        await service.HandleChargeRefundedAsync(refund, CancellationToken.None);

        Assert.NotNull(revokedAt);
        Assert.Equal(revokedAt, grants.ByOrderId.Single().Value.RevokedAt);
        Assert.Equal(1, grants.RevokeCallCount);
    }

    // ---- B3: partial refunds --------------------------------------------------------------------------------------

    private static async Task<(OrderWebhookService Service, Order Order, FakeOrderAccessGrants Grants)>
        PaidAsync(PayoutMode payoutMode, int amountCents = 1000, int platformFeeCents = 50)
    {
        var order = BuildOrder(payoutMode, amountCents, platformFeeCents);
        var (service, _, grants, _) = BuildServiceWithDelivery(order);
        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);
        return (service, order, grants);
    }

    [Fact]
    public async Task APartialRefund_KeepsTheOrderPaid_AndTheBuyersAccess()
    {
        var (service, order, grants) = await PaidAsync(PayoutMode.StripeConnect);

        await service.HandleChargeRefundedAsync(PartialRefund(300), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.True(order.IsPartlyRefunded);
        Assert.Equal(300, order.RefundedCents);
        Assert.Null(Assert.Single(grants.ByOrderId).Value.RevokedAt);
        Assert.Equal(0, grants.RevokeCallCount);
    }

    [Fact]
    public async Task APartialRefundThenTheRest_RefundsTheOrder_AndRevokesTheLink()
    {
        var (service, order, grants) = await PaidAsync(PayoutMode.StripeConnect);

        await service.HandleChargeRefundedAsync(PartialRefund(300), CancellationToken.None);
        await service.HandleChargeRefundedAsync(new OrderChargeRefundedDto("pi_1", "ch_1", 1000, 1000, FullyRefunded: true), CancellationToken.None);

        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(1000, order.RefundedCents);
        Assert.NotNull(Assert.Single(grants.ByOrderId).Value.RevokedAt);
    }

    [Fact]
    public async Task AReplayedOrOutOfOrderPartialRefund_ChangesNothing()
    {
        var (service, order, _) = await PaidAsync(PayoutMode.StripeConnect);
        await service.HandleChargeRefundedAsync(PartialRefund(600), CancellationToken.None);
        var updatedAt = order.UpdatedAt;

        await service.HandleChargeRefundedAsync(PartialRefund(600), CancellationToken.None);
        await service.HandleChargeRefundedAsync(PartialRefund(300), CancellationToken.None);

        Assert.Equal(600, order.RefundedCents);
        Assert.Equal(updatedAt, order.UpdatedAt);
    }

    [Fact]
    public async Task APartialRefundInTheBuyersCurrency_IsAppliedAsItsShareOfTheOrder()
    {
        // Adaptive Pricing: a 15.00 EUR order charged as 30.51 BAM; a third of the BAM charge is refunded.
        var (service, order, _) = await PaidAsync(PayoutMode.StripeConnect, amountCents: 1500, platformFeeCents: 75);

        await service.HandleChargeRefundedAsync(PartialRefund(refunded: 1017, chargeAmount: 3051), CancellationToken.None);

        Assert.Equal(500, order.RefundedCents);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task APartialRefundRoundingToTheFullAmount_StillKeepsTheOrderPaid()
    {
        var (service, order, grants) = await PaidAsync(PayoutMode.StripeConnect);

        await service.HandleChargeRefundedAsync(PartialRefund(refunded: 99_999, chargeAmount: 100_000), CancellationToken.None);

        Assert.Equal((OrderStatus.Paid, 999), (order.Status, order.RefundedCents));
        Assert.Equal(0, grants.RevokeCallCount);
    }

    // ---- B2: the order exists before its session ------------------------------------------------------------------

    private static Order PendingOrderWithoutSession() => Order.Create(
        creatorId: 1, productId: 2, landingPageId: 3, email: "buyer@example.com", name: null, amountCents: 1000, Currency.Eur,
        platformFeeBasisPoints: 500, platformFeeCents: 50, payoutMode: nameof(PayoutMode.StripeConnect),
        stripeCheckoutSessionId: null, DateTimeOffset.UtcNow);

    private static Dictionary<string, string> SaleMetadata(Guid orderPublicId) => new()
    {
        [CheckoutSessionMetadata.OrderId] = orderPublicId.ToString(),
        [CheckoutSessionMetadata.ProductId] = "2",
    };

    [Fact]
    public async Task ACompletedSession_WhoseIdWasNeverStored_FindsItsOrderByTheMetadata_AndLinksIt()
    {
        var order = PendingOrderWithoutSession();
        var (service, _, grants, outbox) = BuildServiceWithDelivery(order);

        await service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto("cs_late", "pi_1", Metadata: SaleMetadata(order.PublicId)), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal("cs_late", order.StripeCheckoutSessionId);
        Assert.Single(grants.ByOrderId);
        Assert.Equal(1, outbox.OrderAccessQueuedCount);
    }

    [Fact]
    public async Task ACompletedSaleSession_WithNoOrder_Throws_SoTheEventIsRecordedAsAFailure()
    {
        var order = PendingOrderWithoutSession();
        var (service, _) = BuildService(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto("cs_lost", "pi_1", Metadata: SaleMetadata(Guid.NewGuid())), CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task ACompletedSession_ForAnOrderTiedToAnotherSession_Throws()
    {
        var order = BuildOrder(PayoutMode.StripeConnect, sessionId: "cs_first");
        var (service, _) = BuildService(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto("cs_second", "pi_1", Metadata: SaleMetadata(order.PublicId)), CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task ACompletedPlanSubscriptionSession_IsNotASale_AndIsIgnored()
    {
        var order = PendingOrderWithoutSession();
        var (service, _) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto("cs_plan", null, Metadata: new Dictionary<string, string> { ["creatorId"] = "1", ["subscriptionId"] = "9" }),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    // ---- B4: an expired session fails its order -------------------------------------------------------------------

    [Fact]
    public async Task AnExpiredSession_FailsItsPendingOrder_Once()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        var (service, _) = BuildService(order);
        var expired = new OrderCheckoutExpiredDto(order.StripeCheckoutSessionId!, SaleMetadata(order.PublicId));

        await service.HandleCheckoutSessionExpiredAsync(expired, CancellationToken.None);
        var failedAt = order.UpdatedAt;
        await service.HandleCheckoutSessionExpiredAsync(expired, CancellationToken.None);

        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal(failedAt, order.UpdatedAt);
    }

    [Fact]
    public async Task AnExpiredSession_WhoseIdWasNeverStored_FailsTheOrderFoundByTheMetadata()
    {
        var order = PendingOrderWithoutSession();
        var (service, _) = BuildService(order);

        await service.HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto("cs_never_stored", SaleMetadata(order.PublicId)), CancellationToken.None);

        Assert.Equal((OrderStatus.Failed, "cs_never_stored"), (order.Status, order.StripeCheckoutSessionId));
    }

    [Fact]
    public async Task AnExpiredSession_LeavesAPaidOrderAlone()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow, buyerEmail: null, buyerName: null);
        var (service, _) = BuildService(order);

        await service.HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto(order.StripeCheckoutSessionId!, SaleMetadata(order.PublicId)), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task AnExpiredSaleSession_WithNoOrder_IsOnlyLogged_AndAPlanSessionIsIgnored()
    {
        var order = PendingOrderWithoutSession();
        var (service, _) = BuildService(order);

        await service.HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto("cs_lost", SaleMetadata(Guid.NewGuid())), CancellationToken.None);
        await service.HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto("cs_plan", new Dictionary<string, string> { ["subscriptionId"] = "9" }), CancellationToken.None);

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task APaidSession_ForAnOrderAlreadyFailed_StillPaysIt()
    {
        // The maintenance job (or a late expiry) failed it, but the buyer did pay: the money wins.
        var order = BuildOrder(PayoutMode.StripeConnect);
        order.MarkFailed(DateTimeOffset.UtcNow);
        var (service, _) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void OnlyAPendingOrder_CanFail()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow, buyerEmail: null, buyerName: null);

        Assert.Throws<InvalidOperationException>(() => order.MarkFailed(DateTimeOffset.UtcNow));
    }
}
