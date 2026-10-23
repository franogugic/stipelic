using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Maintenance;

/// <summary>B4 (job part): checkouts still Pending after 25 h (the default) become Failed; younger, paid and refunded orders are never
/// touched, and an order paid while the job runs stays Paid.</summary>
[Collection(PostgresCollection.Name)]
public sealed class FailStalePendingOrdersTaskTests
{
    private readonly PostgresFixture _postgres;
    private readonly TestData _data;

    public FailStalePendingOrdersTaskTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _data = new TestData(postgres);
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record OrderRow(string Status, DateTimeOffset UpdatedAt);

    private async Task<int> CreateProductAsync(int creatorId)
    {
        await using var db = _postgres.CreateDbContext();
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName",
                                           "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Presets', 2900, 'Active', {$"ProductFile/{creatorId}/{Guid.NewGuid()}.zip"},
                    'Presets.zip', 1024, 'application/zip', now(), now(), now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task<int> CreateOrderAsync(int creatorId, int productId, string status, DateTimeOffset createdAt)
    {
        await using var db = _postgres.CreateDbContext();
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {TestData.UniqueEmail("buyer")}, 2900, 'Eur', {status}, 0, 0,
                    'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {createdAt}, {createdAt})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task<OrderRow> GetAsync(int orderId)
    {
        await using var db = _postgres.CreateDbContext();
        return (await db.Database.SqlQuery<OrderRow>($"""
            SELECT "Status", "UpdatedAt" FROM orders.orders WHERE "Id" = {orderId}
            """).ToListAsync()).Single();
    }

    private FailStalePendingOrdersTask CreateTask(int batchSize = 1000) =>
        new(_postgres.CreateDbContext(), Options.Create(new MaintenanceOptions { BatchSize = batchSize }));

    [Fact]
    public async Task CheckoutsPendingForOver25Hours_BecomeFailed_InBatches_AndNothingElseChanges()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await CreateProductAsync(creator.CreatorId);
        var stale = new[]
        {
            await CreateOrderAsync(creator.CreatorId, productId, "Pending", Now.AddHours(-26)),
            await CreateOrderAsync(creator.CreatorId, productId, "Pending", Now.AddDays(-3)),
            await CreateOrderAsync(creator.CreatorId, productId, "Pending", Now.AddDays(-40)),
        };
        var young = await CreateOrderAsync(creator.CreatorId, productId, "Pending", Now.AddHours(-24));   // session just expired: the hour of margin
        var paid = await CreateOrderAsync(creator.CreatorId, productId, "Paid", Now.AddDays(-3));
        var refunded = await CreateOrderAsync(creator.CreatorId, productId, "Refunded", Now.AddDays(-3));

        var failed = await CreateTask(batchSize: 1).RunAsync(Now, CancellationToken.None);

        Assert.True(failed >= 3);
        foreach (var orderId in stale)
        {
            var row = await GetAsync(orderId);
            Assert.Equal("Failed", row.Status);
            Assert.Equal(Now, row.UpdatedAt, TimeSpan.FromMilliseconds(1));
        }

        Assert.Equal("Pending", (await GetAsync(young)).Status);
        Assert.Equal("Paid", (await GetAsync(paid)).Status);
        Assert.Equal("Refunded", (await GetAsync(refunded)).Status);
        Assert.Equal(0, await CreateTask().RunAsync(Now, CancellationToken.None));
    }

    [Fact]
    public async Task AnOrderPaidWhileTheJobRuns_StaysPaid()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await CreateProductAsync(creator.CreatorId);
        var orderId = await CreateOrderAsync(creator.CreatorId, productId, "Pending", Now.AddDays(-2));

        // The webhook: holds the order row (as GetByStripeCheckoutSessionIdForUpdateAsync does) while it pays it.
        await using var webhook = _postgres.CreateDbContext();
        await using var transaction = await webhook.Database.BeginTransactionAsync();
        await webhook.Database.ExecuteSqlAsync($"""SELECT 1 FROM orders.orders WHERE "Id" = {orderId} FOR UPDATE""");

        var job = CreateTask().RunAsync(Now, CancellationToken.None);
        await WaitUntilAQueryWaitsForALockAsync();

        await webhook.Database.ExecuteSqlAsync($"""UPDATE orders.orders SET "Status" = 'Paid' WHERE "Id" = {orderId}""");
        await transaction.CommitAsync();
        await job;

        Assert.Equal("Paid", (await GetAsync(orderId)).Status);
    }

    [Theory]
    [InlineData(23, false)]   // a Checkout session can still be paid
    [InlineData(24, true)]
    [InlineData(720, true)]
    [InlineData(721, false)]
    public void TheAge_CannotBeShorterThanACheckoutSessionLives(int hours, bool valid)
    {
        var result = new MaintenanceOptionsValidator().Validate(null, new MaintenanceOptions { StalePendingOrderHours = hours });

        Assert.Equal(valid, result.Succeeded);
    }

    private async Task WaitUntilAQueryWaitsForALockAsync()
    {
        await using var db = _postgres.CreateDbContext();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var waiting = await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" FROM pg_stat_activity
                WHERE datname = current_database() AND wait_event_type = 'Lock'
                """).SingleAsync();
            if (waiting > 0)
                return;
            await Task.Delay(50);
        }

        throw new TimeoutException("The job never waited for the webhook's row lock.");
    }
}
