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
        var orderRepository = new FakeWebhookOrderRepository { Order = order };
        var ledgerRepository = new FakeLedgerEntryRepository();
        var payoutLedgerService = new PayoutLedgerService(ledgerRepository, NullLogger<PayoutLedgerService>.Instance);
        var contextProvider = new FakeOrdersCreatorContextProvider();
        var unitOfWork = new FakeOrdersUnitOfWork();
        var emailOutbox = new FakeEmailOutboxService();
        var homeSummaryCache = new FakeHomeSummaryCache();
        var options = Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.example.com", ApiBaseUrl = "https://api.example.com" });

        var service = new OrderWebhookService(
            orderRepository,
            unitOfWork,
            contextProvider,
            emailOutbox,
            homeSummaryCache,
            payoutLedgerService,
            options,
            NullLogger<OrderWebhookService>.Instance);

        return (service, orderRepository, ledgerRepository);
    }

    [Fact]
    public async Task HandleCheckoutSessionCompleted_BankTransfer_BooksTwoEntries()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        var (service, orderRepository, ledgerRepository) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId, "pi_1"), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, orderRepository.Order!.Status);
        Assert.Equal(2, ledgerRepository.Entries.Count);
    }

    [Fact]
    public async Task HandleCheckoutSessionCompleted_BankTransferZeroFee_BooksOneEntry()
    {
        var order = BuildOrder(PayoutMode.BankTransfer, platformFeeCents: 0);
        var (service, _, ledgerRepository) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId, "pi_1"), CancellationToken.None);

        Assert.Single(ledgerRepository.Entries);
    }

    [Fact]
    public async Task HandleCheckoutSessionCompleted_StripeConnect_BooksNoEntries()
    {
        var order = BuildOrder(PayoutMode.StripeConnect);
        var (service, orderRepository, ledgerRepository) = BuildService(order);

        await service.HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId, "pi_1"), CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, orderRepository.Order!.Status);
        Assert.Empty(ledgerRepository.Entries);
    }

    [Fact]
    public async Task HandleCheckoutSessionCompleted_CalledTwice_RemainsIdempotent()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        var (service, _, ledgerRepository) = BuildService(order);
        var data = new OrderCheckoutCompletedDto(order.StripeCheckoutSessionId, "pi_1");

        await service.HandleCheckoutSessionCompletedAsync(data, CancellationToken.None);
        await service.HandleCheckoutSessionCompletedAsync(data, CancellationToken.None);

        Assert.Equal(2, ledgerRepository.Entries.Count);
    }

    [Fact]
    public async Task HandleChargeRefunded_BankTransfer_BooksReversalEntries()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow);
        var (service, orderRepository, ledgerRepository) = BuildService(order);

        await service.HandleChargeRefundedAsync(new OrderChargeRefundedDto("pi_1", "ch_1"), CancellationToken.None);

        Assert.Equal(OrderStatus.Refunded, orderRepository.Order!.Status);
        Assert.Equal(2, ledgerRepository.Entries.Count);
        Assert.Contains(ledgerRepository.Entries, e => e.Type == LedgerEntryType.RefundDebit);
        Assert.Contains(ledgerRepository.Entries, e => e.Type == LedgerEntryType.FeeRefundCredit);
    }

    [Fact]
    public async Task HandleChargeRefunded_CalledTwice_RemainsIdempotent()
    {
        var order = BuildOrder(PayoutMode.BankTransfer);
        order.MarkPaid("pi_1", DateTimeOffset.UtcNow);
        var (service, _, ledgerRepository) = BuildService(order);
        var data = new OrderChargeRefundedDto("pi_1", "ch_1");

        await service.HandleChargeRefundedAsync(data, CancellationToken.None);
        await service.HandleChargeRefundedAsync(data, CancellationToken.None);

        Assert.Equal(2, ledgerRepository.Entries.Count);
    }
}
