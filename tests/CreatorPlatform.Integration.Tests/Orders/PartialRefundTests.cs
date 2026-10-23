using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Persistence;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Payouts.Infrastructure.Repositories;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>B3 against the real database: a partial refund keeps the order Paid and the link on, books its share in the
/// ledger, and the rest of the refund reverses the whole sale; the (OrderId, Type, RefundedToCents) key and the checks;
/// the migration's backfill of orders and ledger entries refunded before partial refunds existed.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PartialRefundTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public PartialRefundTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private static OrderWebhookService Webhooks(CreatorPlatformDbContext db) => new(
        new OrderRepository(db),
        new OrdersUnitOfWork(db),
        new CreatorContextProvider(db),
        new EmailOutboxService(db, Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.luma.test" })),
        new NoHomeSummaryCache(),
        new PayoutLedgerService(new LedgerEntryRepository(db), NullLogger<PayoutLedgerService>.Instance),
        new OrderAccessGrants(db),
        Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.luma.test" }),
        NullLogger<OrderWebhookService>.Instance);

    /// <summary>A paid 50.00 EUR bank-transfer order with a 2.50 fee, its link and its sale entries; returns its id.</summary>
    private async Task<(int OrderId, string PaymentIntentId)> PaidOrderAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        var sessionId = $"cs_test_{Guid.NewGuid():N}";
        await using (var db = _fixture.CreateDbContext())
        {
            var productId = (await db.Database.SqlQuery<int>($"""
                INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName",
                                               "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {creator.CreatorId}, 'Course', 5000, 'Active', 'ProductFile/t/c.zip', 'c.zip', 1024,
                        'application/zip', now(), now(), now())
                RETURNING "Id" AS "Value"
                """).ToListAsync()).Single();
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency", "Status",
                                           "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                           "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {creator.CreatorId}, {productId}, 'buyer@example.test', 5000, 'Eur', 'Pending', 500, 250,
                        'BankTransfer', {sessionId}, now(), now())
                """);
        }

        var paymentIntentId = $"pi_{Guid.NewGuid():N}";
        await using (var db = _fixture.CreateDbContext())
            await Webhooks(db).HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(sessionId, paymentIntentId), None);

        await using var read = _fixture.CreateDbContext();
        var orderId = (await read.Database.SqlQuery<int>($"""
            SELECT "Id" AS "Value" FROM orders.orders WHERE "StripeCheckoutSessionId" = {sessionId}
            """).ToListAsync()).Single();
        return (orderId, paymentIntentId);
    }

    private async Task RefundAsync(string paymentIntentId, long refunded, bool full = false)
    {
        await using var db = _fixture.CreateDbContext();
        await Webhooks(db).HandleChargeRefundedAsync(new OrderChargeRefundedDto(paymentIntentId, "ch_1", 5000, refunded, full), None);
    }

    private sealed record OrderRow(string Status, int RefundedCents, DateTimeOffset? RevokedAt);

    private async Task<OrderRow> OrderAsync(int orderId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<OrderRow>($"""
            SELECT o."Status", o."RefundedCents", g."RevokedAt"
            FROM orders.orders o JOIN products.access_grants g ON g."OrderId" = o."Id"
            WHERE o."Id" = {orderId}
            """).ToListAsync()).Single();
    }

    private sealed record EntryRow(string Type, int AmountCents, int? RefundedToCents);

    private async Task<List<EntryRow>> EntriesAsync(int orderId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<EntryRow>($"""
            SELECT "Type", "AmountCents", "RefundedToCents" FROM payouts.ledger_entries WHERE "OrderId" = {orderId} ORDER BY "Id"
            """).ToListAsync();
    }

    [Fact]
    public async Task APartialRefund_ThenTheRest_BooksEachStepOnce_AndOnlyTheFullRefundRevokes()
    {
        var (orderId, paymentIntentId) = await PaidOrderAsync();

        await RefundAsync(paymentIntentId, 1000);
        await RefundAsync(paymentIntentId, 1000);   // Stripe redelivers the same event

        Assert.Equal(new OrderRow("Paid", 1000, null), await OrderAsync(orderId));
        Assert.Equal(
            [new("SaleCredit", 5000, null), new("FeeDebit", -250, null), new("RefundDebit", -1000, 1000), new("FeeRefundCredit", 50, 1000)],
            await EntriesAsync(orderId));

        await RefundAsync(paymentIntentId, 5000, full: true);

        var refunded = await OrderAsync(orderId);
        Assert.Equal(("Refunded", 5000), (refunded.Status, refunded.RefundedCents));
        Assert.NotNull(refunded.RevokedAt);
        var entries = await EntriesAsync(orderId);
        Assert.Equal([new("RefundDebit", -4000, 5000), new("FeeRefundCredit", 200, 5000)], entries.Skip(4));
        Assert.Equal(0, entries.Sum(e => e.AmountCents));
    }

    [Fact]
    public async Task TheDatabase_RefusesASecondRefundEntryForTheSameStep_AndARefundAboveTheAmount()
    {
        var (orderId, paymentIntentId) = await PaidOrderAsync();
        await RefundAsync(paymentIntentId, 1000);
        await using var db = _fixture.CreateDbContext();

        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO payouts.ledger_entries ("PublicId", "CreatorId", "OrderId", "Type", "AmountCents", "Currency", "CreatedAt", "RefundedToCents")
            SELECT {Guid.NewGuid()}, "CreatorId", "Id", 'RefundDebit', -1000, 'Eur', now(), 1000 FROM orders.orders WHERE "Id" = {orderId}
            """));
        Assert.Equal("IX_ledger_entries_OrderId_Type_RefundedToCents", duplicate.ConstraintName);

        var secondSale = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO payouts.ledger_entries ("PublicId", "CreatorId", "OrderId", "Type", "AmountCents", "Currency", "CreatedAt")
            SELECT {Guid.NewGuid()}, "CreatorId", "Id", 'SaleCredit', 5000, 'Eur', now() FROM orders.orders WHERE "Id" = {orderId}
            """));
        Assert.Equal("IX_ledger_entries_OrderId_Type_RefundedToCents", secondSale.ConstraintName);

        var unkeyedRefund = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO payouts.ledger_entries ("PublicId", "CreatorId", "OrderId", "Type", "AmountCents", "Currency", "CreatedAt")
            SELECT {Guid.NewGuid()}, "CreatorId", "Id", 'RefundDebit', -10, 'Eur', now() FROM orders.orders WHERE "Id" = {orderId}
            """));
        Assert.Equal("CK_ledger_entries_RefundedToCents_OnRefundsOnly", unkeyedRefund.ConstraintName);

        var overRefund = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            UPDATE orders.orders SET "RefundedCents" = 5001 WHERE "Id" = {orderId}
            """));
        Assert.Equal("CK_orders_RefundedCents_WithinAmount", overRefund.ConstraintName);
    }

    [Fact]
    public async Task TheMigrationStep_MarksEarlierRefundsAsFull_AndIsIdempotent()
    {
        var (orderId, paymentIntentId) = await PaidOrderAsync();
        await RefundAsync(paymentIntentId, 5000, full: true);

        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // The state before the migration: no refunded amount anywhere. Rolled back with the transaction.
        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE orders.orders DROP CONSTRAINT "CK_orders_RefundedCents_WithinAmount" """);
        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE payouts.ledger_entries DROP CONSTRAINT "CK_ledger_entries_RefundedToCents_OnRefundsOnly" """);
        await db.Database.ExecuteSqlAsync($"""UPDATE orders.orders SET "RefundedCents" = 0 WHERE "Id" = {orderId}""");
        await db.Database.ExecuteSqlAsync($"""UPDATE payouts.ledger_entries SET "RefundedToCents" = NULL WHERE "OrderId" = {orderId}""");

        for (var run = 0; run < 2; run++)
        {
            await db.Database.ExecuteSqlRawAsync(RecordPartialRefunds.BackfillRefundedOrdersSql);
            await db.Database.ExecuteSqlRawAsync(RecordPartialRefunds.BackfillRefundEntriesSql);
        }

        var refundedCents = (await db.Database.SqlQuery<int>($"""
            SELECT "RefundedCents" AS "Value" FROM orders.orders WHERE "Id" = {orderId}
            """).ToListAsync()).Single();
        Assert.Equal(5000, refundedCents);
        var keys = await db.Database.SqlQuery<int?>($"""
            SELECT "RefundedToCents" AS "Value" FROM payouts.ledger_entries WHERE "OrderId" = {orderId} ORDER BY "Id"
            """).ToListAsync();
        Assert.Equal([null, null, 5000, 5000], keys);

        await transaction.RollbackAsync();
    }

    private sealed class NoHomeSummaryCache : IHomeSummaryCache
    {
        public ValueTask<HomeSummaryDto?> GetAsync(int creatorId, CancellationToken ct) => ValueTask.FromResult<HomeSummaryDto?>(null);
        public Task SetAsync(int creatorId, HomeSummaryDto value, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(int creatorId, CancellationToken ct) => Task.CompletedTask;
    }
}
