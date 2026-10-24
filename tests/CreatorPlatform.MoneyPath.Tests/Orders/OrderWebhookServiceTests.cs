using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Payouts.Domain.Payouts;
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

    private static (OrderWebhookService Service, FakeWebhookOrderRepository OrderRepository, FakeLedgerEntryRepository LedgerRepository)
        BuildService(Order order)
    {
        var (service, orderRepository, ledgerRepository, _, _) = BuildServiceWithDelivery(order);
        return (service, orderRepository, ledgerRepository);
    }

    private static (OrderWebhookService Service, FakeWebhookOrderRepository OrderRepository, FakeLedgerEntryRepository LedgerRepository,
        FakeOrderAccessGrants Grants, FakeEmailOutboxService Outbox)
        BuildServiceWithDelivery(Order order)
    {
        var orderRepository = new FakeWebhookOrderRepository { Order = order };
        var ledgerRepository = new FakeLedgerEntryRepository();
        var payoutLedgerService = new PayoutLedgerService(ledgerRepository, NullLogger<PayoutLedgerService>.Instance);
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
            payoutLedgerService,
            grants,
            options,
            NullLogger<OrderWebhookService>.Instance);

        return (service, orderRepository, ledgerRepository, grants, emailOutbox);
    }

    /// <summary>charge.refunded for the default 1000-cent order, charged in the order's own currency.</summary>
    private static OrderChargeRefundedDto FullRefund() => new("pi_1", "ch_1", 1000, 1000, FullyRefunded: true);

    private static OrderChargeRefundedDto PartialRefund(long refunded, long chargeAmount = 1000) =>
        new("pi_1", "ch_1", chargeAmount, refunded, FullyRefunded: false);

    [Fact]
    public async Task HandleCheckoutSessionCompleted_BankTransfer_BooksTwoEntries()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        var (service, orderRepository, ledgerRepository) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, orderRepository.Order!.Status);
        Assert.Equal(2, ledgerRepository.Entries.Count);
    }

    [Fact]
    public async Task HandleCheckoutSessionCompleted_BankTransferZeroFee_BooksOneEntry()
    {
        var order = BuildOrder(PayoutMode.BankTransfer, platformFeeCents: 0);
        var (service, _, ledgerRepository) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);

        Assert.Single(ledgerRepository.Entries);
    }

    [Fact]
    public async Task HandleCheckoutSessionCompleted_StripeConnect_BooksNoEntries()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        var (service, orderRepository, ledgerRepository) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, orderRepository.Order!.Status);
        Assert.Empty(ledgerRepository.Entries);
    }

    [Fact]
    public async Task HandleCheckoutSessionCompleted_CalledTwice_RemainsIdempotent()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        var (service, _, ledgerRepository) = BuildService(order);
        var data = new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1");

        await service.HandleCheckoutSessionCompletedAsync(data, CancellationToken.None);
        await service.HandleCheckoutSessionCompletedAsync(data, CancellationToken.None);

        Assert.Equal(2, ledgerRepository.Entries.Count);
    }

    [Fact]
    public async Task HandleChargeRefunded_BankTransfer_BooksReversalEntries()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow, buyerEmail: null, buyerName: null);
        var (service, orderRepository, ledgerRepository) = BuildService(order);

        await service.HandleChargeRefundedAsync(FullRefund(), CancellationToken.None);

        Assert.Equal(OrderStatus.Refunded, orderRepository.Order!.Status);
        Assert.Equal(2, ledgerRepository.Entries.Count);
        Assert.Contains(ledgerRepository.Entries, e => e.Type == LedgerEntryType.RefundDebit);
        Assert.Contains(ledgerRepository.Entries, e => e.Type == LedgerEntryType.FeeRefundCredit);
    }

    [Fact]
    public async Task HandleChargeRefunded_CalledTwice_RemainsIdempotent()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow, buyerEmail: null, buyerName: null);
        var (service, _, ledgerRepository) = BuildService(order);
        var data = FullRefund();

        await service.HandleChargeRefundedAsync(data, CancellationToken.None);
        await service.HandleChargeRefundedAsync(data, CancellationToken.None);

        Assert.Equal(2, ledgerRepository.Entries.Count);
    }

    // ---- F8: the buyer's personal link ---------------------------------------------------------------------------

    [Fact]
    public async Task APaidOrder_GetsExactlyOneGrant_AndItsEmailLinksTheDownloadPage()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        var (service, _, _, grants, outbox) = BuildServiceWithDelivery(order);
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
        var order = BuildOrder(PayoutMode.BankTransfer);
        var (service, _, _, grants, _) = BuildServiceWithDelivery(order);
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

    private static async Task<(OrderWebhookService Service, Order Order, FakeLedgerEntryRepository Ledger, FakeOrderAccessGrants Grants)>
        PaidAsync(PayoutMode payoutMode, int amountCents = 1000, int platformFeeCents = 50)
    {
        var order = BuildOrder(payoutMode, amountCents, platformFeeCents);
        var (service, _, ledger, grants, _) = BuildServiceWithDelivery(order);
        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);
        return (service, order, ledger, grants);
    }

    [Fact]
    public async Task APartialRefund_KeepsTheOrderPaid_AndTheBuyersAccess_AndReversesOnlyItsShare()
    {
        var (service, order, ledger, grants) = await PaidAsync(PayoutMode.BankTransfer);

        await service.HandleChargeRefundedAsync(PartialRefund(300), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.True(order.IsPartlyRefunded);
        Assert.Equal(300, order.RefundedCents);
        Assert.Null(Assert.Single(grants.ByOrderId).Value.RevokedAt);
        Assert.Equal(0, grants.RevokeCallCount);
        Assert.Equal(-300, Assert.Single(ledger.Entries, e => e.Type == LedgerEntryType.RefundDebit).AmountCents);
        Assert.Equal(15, Assert.Single(ledger.Entries, e => e.Type == LedgerEntryType.FeeRefundCredit).AmountCents);
        Assert.Equal(1000 - 50 - 300 + 15, ledger.Entries.Sum(e => e.AmountCents));
    }

    [Fact]
    public async Task APartialRefundThenTheRest_RefundsTheOrder_RevokesTheLink_AndReversesTheWholeSale()
    {
        var (service, order, ledger, grants) = await PaidAsync(PayoutMode.BankTransfer);

        await service.HandleChargeRefundedAsync(PartialRefund(300), CancellationToken.None);
        await service.HandleChargeRefundedAsync(new OrderChargeRefundedDto("pi_1", "ch_1", 1000, 1000, FullyRefunded: true), CancellationToken.None);

        Assert.Equal(OrderStatus.Refunded, order.Status);
        Assert.Equal(1000, order.RefundedCents);
        Assert.NotNull(Assert.Single(grants.ByOrderId).Value.RevokedAt);
        Assert.Equal(0, ledger.Entries.Sum(e => e.AmountCents));
    }

    [Fact]
    public async Task AReplayedOrOutOfOrderPartialRefund_ChangesNothing()
    {
        var (service, order, ledger, _) = await PaidAsync(PayoutMode.BankTransfer);
        await service.HandleChargeRefundedAsync(PartialRefund(600), CancellationToken.None);
        var updatedAt = order.UpdatedAt;

        await service.HandleChargeRefundedAsync(PartialRefund(600), CancellationToken.None);
        await service.HandleChargeRefundedAsync(PartialRefund(300), CancellationToken.None);

        Assert.Equal(600, order.RefundedCents);
        Assert.Equal(updatedAt, order.UpdatedAt);
        Assert.Equal(4, ledger.Entries.Count);
    }

    [Fact]
    public async Task APartialRefundInTheBuyersCurrency_IsAppliedAsItsShareOfTheOrder()
    {
        // Adaptive Pricing: a 15.00 EUR order charged as 30.51 BAM; a third of the BAM charge is refunded.
        var (service, order, ledger, _) = await PaidAsync(PayoutMode.BankTransfer, amountCents: 1500, platformFeeCents: 75);

        await service.HandleChargeRefundedAsync(PartialRefund(refunded: 1017, chargeAmount: 3051), CancellationToken.None);

        Assert.Equal(500, order.RefundedCents);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(-500, Assert.Single(ledger.Entries, e => e.Type == LedgerEntryType.RefundDebit).AmountCents);
    }

    [Fact]
    public async Task APartialRefundRoundingToTheFullAmount_StillKeepsTheOrderPaid()
    {
        var (service, order, _, grants) = await PaidAsync(PayoutMode.StripeConnect);

        await service.HandleChargeRefundedAsync(PartialRefund(refunded: 99_999, chargeAmount: 100_000), CancellationToken.None);

        Assert.Equal((OrderStatus.Paid, 999), (order.Status, order.RefundedCents));
        Assert.Equal(0, grants.RevokeCallCount);
    }

    [Fact]
    public async Task APartialRefundOnAConnectOrder_BooksNoLedgerEntries()
    {
        var (service, order, ledger, _) = await PaidAsync(PayoutMode.StripeConnect);

        await service.HandleChargeRefundedAsync(PartialRefund(400), CancellationToken.None);

        Assert.Equal((OrderStatus.Paid, 400), (order.Status, order.RefundedCents));
        Assert.Empty(ledger.Entries);
    }

    // ---- B2: the order exists before its session ------------------------------------------------------------------

    private static Order PendingOrderWithoutSession() => Order.Create(
        creatorId: 1, productId: 2, landingPageId: 3, email: "buyer@example.com", name: null, amountCents: 1000, Currency.Eur,
        platformFeeBasisPoints: 500, platformFeeCents: 50, payoutMode: nameof(PayoutMode.BankTransfer),
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
        var (service, _, ledger, grants, outbox) = BuildServiceWithDelivery(order);

        await service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto("cs_late", "pi_1", Metadata: SaleMetadata(order.PublicId)), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal("cs_late", order.StripeCheckoutSessionId);
        Assert.Equal(2, ledger.Entries.Count);
        Assert.Single(grants.ByOrderId);
        Assert.Equal(1, outbox.OrderAccessQueuedCount);
    }

    [Fact]
    public async Task ACompletedSaleSession_WithNoOrder_Throws_SoTheEventIsRecordedAsAFailure()
    {
        var order = PendingOrderWithoutSession();
        var (service, _, ledger) = BuildService(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto("cs_lost", "pi_1", Metadata: SaleMetadata(Guid.NewGuid())), CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public async Task ACompletedSession_ForAnOrderTiedToAnotherSession_Throws()
    {
        var order = BuildOrder(PayoutMode.BankTransfer, sessionId: "cs_first");
        var (service, _, _) = BuildService(order);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto("cs_second", "pi_1", Metadata: SaleMetadata(order.PublicId)), CancellationToken.None));

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task ACompletedPlanSubscriptionSession_IsNotASale_AndIsIgnored()
    {
        var order = PendingOrderWithoutSession();
        var (service, _, ledger) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(
            new OrderCheckoutCompletedDto("cs_plan", null, Metadata: new Dictionary<string, string> { ["creatorId"] = "1", ["subscriptionId"] = "9" }),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Empty(ledger.Entries);
    }

    // ---- B4: an expired session fails its order -------------------------------------------------------------------

    [Fact]
    public async Task AnExpiredSession_FailsItsPendingOrder_Once()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        var (service, _, ledger) = BuildService(order);
        var expired = new OrderCheckoutExpiredDto(order.StripeCheckoutSessionId!, SaleMetadata(order.PublicId));

        await service.HandleCheckoutSessionExpiredAsync(expired, CancellationToken.None);
        var failedAt = order.UpdatedAt;
        await service.HandleCheckoutSessionExpiredAsync(expired, CancellationToken.None);

        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal(failedAt, order.UpdatedAt);
        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public async Task AnExpiredSession_WhoseIdWasNeverStored_FailsTheOrderFoundByTheMetadata()
    {
        var order = PendingOrderWithoutSession();
        var (service, _, _) = BuildService(order);

        await service.HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto("cs_never_stored", SaleMetadata(order.PublicId)), CancellationToken.None);

        Assert.Equal((OrderStatus.Failed, "cs_never_stored"), (order.Status, order.StripeCheckoutSessionId));
    }

    [Fact]
    public async Task AnExpiredSession_LeavesAPaidOrderAlone()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow, buyerEmail: null, buyerName: null);
        var (service, _, _) = BuildService(order);

        await service.HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto(order.StripeCheckoutSessionId!, SaleMetadata(order.PublicId)), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public async Task AnExpiredSaleSession_WithNoOrder_IsOnlyLogged_AndAPlanSessionIsIgnored()
    {
        var order = PendingOrderWithoutSession();
        var (service, _, _) = BuildService(order);

        await service.HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto("cs_lost", SaleMetadata(Guid.NewGuid())), CancellationToken.None);
        await service.HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto("cs_plan", new Dictionary<string, string> { ["subscriptionId"] = "9" }), CancellationToken.None);

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task APaidSession_ForAnOrderAlreadyFailed_StillPaysIt()
    {
        // The maintenance job (or a late expiry) failed it, but the buyer did pay: the money wins.
        var order = BuildOrder(PayoutMode.BankTransfer);
        order.MarkFailed(DateTimeOffset.UtcNow);
        var (service, _, ledger) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId!, "pi_1"), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(2, ledger.Entries.Count);
    }

    [Fact]
    public void OnlyAPendingOrder_CanFail()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow, buyerEmail: null, buyerName: null);

        Assert.Throws<InvalidOperationException>(() => order.MarkFailed(DateTimeOffset.UtcNow));
    }
}
