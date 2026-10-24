using CreatorPlatform.Creators.Domain.Creators;
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
using CreatorPlatform.Shared.Domain.Enums;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>B2 against the real database: the Pending order is committed before Stripe is called, a failed Stripe call
/// leaves it Failed, and checkout.session.completed for a session whose id was never stored finds the order by the
/// metadata and pays it; an expired session fails its order (B4); the database refuses a paid order without a session.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CheckoutOrderFirstTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public CheckoutOrderFirstTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private sealed record Seeded(int CreatorId, int ProductId, int LandingPageId);

    private async Task<Seeded> SeedAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        var landingPageId = await _data.CreateLandingPageAsync(creator.CreatorId);
        await using var db = _fixture.CreateDbContext();
        var productId = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName",
                                           "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creator.CreatorId}, 'Course', 5000, 'Active', 'ProductFile/t/c.zip', 'c.zip', 1024,
                    'application/zip', now(), now(), now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return new Seeded(creator.CreatorId, productId, landingPageId);
    }

    /// <summary>The real provider, except the checkout lookup returns a sellable product (the readiness rules are not under test).</summary>
    private sealed class SellableProductProvider(CreatorContextProvider inner, Seeded seeded) : ICreatorContextProvider
    {
        public Task<LandingPageProductInfo?> GetProductInfoByLandingPageSlugAsync(string creatorSlug, string landingPageSlug, CancellationToken ct)
            => Task.FromResult<LandingPageProductInfo?>(new LandingPageProductInfo(
                seeded.CreatorId, seeded.ProductId, seeded.LandingPageId, "Course", null, 5000, Currency.Eur, CreatorStatus.Active,
                PayoutMode.StripeConnect, "acct_test", true, PlatformFeeBasisPoints: 500));

        public Task<OrderEmailContext?> GetOrderEmailContextAsync(int productId, CancellationToken ct)
            => inner.GetOrderEmailContextAsync(productId, ct);
    }

    /// <summary>Stripe stand-in: records the metadata and runs a probe at the moment of the call.</summary>
    private sealed class RecordingSessions : IPaymentCheckoutSessionService
    {
        public Func<Task>? Probe { get; init; }
        public Exception? Failure { get; init; }
        public IReadOnlyDictionary<string, string>? Metadata { get; private set; }
        public string SessionId { get; } = $"cs_test_{Guid.NewGuid():N}";

        public async Task<PaymentCheckoutSessionDto> CreateAsync(string productName, int priceCents, string currency, string? customerEmail,
            string successUrl, string cancelUrl, string idempotencyKey, IReadOnlyDictionary<string, string> metadata, int applicationFeeAmountCents, string destinationAccountId,
            CancellationToken ct, string? thumbnailUrl = null)
        {
            Metadata = metadata;
            if (Probe is not null)
                await Probe();
            if (Failure is not null)
                throw Failure;
            return new PaymentCheckoutSessionDto(SessionId, $"https://checkout.stripe.com/{SessionId}");
        }
    }

    private static OrderCheckoutService Checkout(CreatorPlatformDbContext db, Seeded seeded, IPaymentCheckoutSessionService sessions) => new(
        new SellableProductProvider(new CreatorContextProvider(db), seeded),
        sessions,
        new OrderRepository(db),
        new OrdersUnitOfWork(db),
        Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.luma.test" }),
        NullLogger<OrderCheckoutService>.Instance);

    private static OrderWebhookService Webhooks(CreatorPlatformDbContext db) => new(
        new OrderRepository(db),
        new OrdersUnitOfWork(db),
        new CreatorContextProvider(db),
        new EmailOutboxService(db, Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.luma.test" })),
        new NoHomeSummaryCache(),
        new OrderAccessGrants(db),
        Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.luma.test" }),
        NullLogger<OrderWebhookService>.Instance);

    private sealed record OrderRow(string Status, string? StripeCheckoutSessionId);

    private async Task<List<OrderRow>> OrdersAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<OrderRow>($"""
            SELECT "Status", "StripeCheckoutSessionId" FROM orders.orders WHERE "CreatorId" = {creatorId} ORDER BY "Id"
            """).ToListAsync();
    }

    [Fact]
    public async Task ThePendingOrder_IsCommittedBeforeStripeIsCalled_ThenGetsItsSession()
    {
        var seeded = await SeedAsync();
        List<OrderRow>? atStripeCall = null;
        var sessions = new RecordingSessions { Probe = async () => atStripeCall = await OrdersAsync(seeded.CreatorId) };

        await using (var db = _fixture.CreateDbContext())
            await Checkout(db, seeded, sessions).CreateCheckoutAsync("acme", "sale", "buyer@example.test", None);

        Assert.Equal([new OrderRow("Pending", null)], atStripeCall);
        Assert.Equal([new OrderRow("Pending", sessions.SessionId)], await OrdersAsync(seeded.CreatorId));
    }

    [Fact]
    public async Task AFailedStripeCall_LeavesTheOrderFailed()
    {
        var seeded = await SeedAsync();
        var sessions = new RecordingSessions { Failure = new HttpRequestException("Stripe unreachable") };

        await using (var db = _fixture.CreateDbContext())
            await Assert.ThrowsAsync<HttpRequestException>(() =>
                Checkout(db, seeded, sessions).CreateCheckoutAsync("acme", "sale", "buyer@example.test", None));

        Assert.Equal([new OrderRow("Failed", null)], await OrdersAsync(seeded.CreatorId));
    }

    [Fact]
    public async Task ASessionWhoseIdWasNeverStored_IsPaidThroughTheMetadata()
    {
        var seeded = await SeedAsync();
        // Stripe answered, but storing its session id failed: the order is committed without one.
        var sessions = new RecordingSessions();
        await using (var db = _fixture.CreateDbContext())
        {
            await Checkout(db, seeded, sessions).CreateCheckoutAsync("acme", "sale", "buyer@example.test", None);
            await db.Database.ExecuteSqlAsync($"""
                UPDATE orders.orders SET "StripeCheckoutSessionId" = NULL WHERE "CreatorId" = {seeded.CreatorId}
                """);
        }

        await using (var db = _fixture.CreateDbContext())
            await Webhooks(db).HandleCheckoutSessionCompletedAsync(
                new OrderCheckoutCompletedDto(sessions.SessionId, "pi_late", Metadata: sessions.Metadata), None);

        Assert.Equal([new OrderRow("Paid", sessions.SessionId)], await OrdersAsync(seeded.CreatorId));
        await using var read = _fixture.CreateDbContext();
        var grants = (await read.Database.SqlQuery<int>($"""
            SELECT COUNT(*)::int AS "Value" FROM products.access_grants WHERE "CreatorId" = {seeded.CreatorId}
            """).ToListAsync()).Single();
        Assert.Equal(1, grants);
    }

    [Fact]
    public async Task AnExpiredSession_FailsItsOrder()
    {
        var seeded = await SeedAsync();
        var sessions = new RecordingSessions();
        await using (var db = _fixture.CreateDbContext())
            await Checkout(db, seeded, sessions).CreateCheckoutAsync("acme", "sale", "buyer@example.test", None);

        await using (var db = _fixture.CreateDbContext())
            await Webhooks(db).HandleCheckoutSessionExpiredAsync(new OrderCheckoutExpiredDto(sessions.SessionId, sessions.Metadata), None);

        Assert.Equal([new OrderRow("Failed", sessions.SessionId)], await OrdersAsync(seeded.CreatorId));
    }

    [Fact]
    public async Task TheDatabase_RefusesAPaidOrderWithoutASession()
    {
        var seeded = await SeedAsync();
        await using (var db = _fixture.CreateDbContext())
            await Checkout(db, seeded, new RecordingSessions()).CreateCheckoutAsync("acme", "sale", "buyer@example.test", None);

        await using var write = _fixture.CreateDbContext();
        var refused = await Assert.ThrowsAsync<PostgresException>(() => write.Database.ExecuteSqlAsync($"""
            UPDATE orders.orders SET "StripeCheckoutSessionId" = NULL, "Status" = 'Paid' WHERE "CreatorId" = {seeded.CreatorId}
            """));
        Assert.Equal("CK_orders_Session_Unless_PendingOrFailed", refused.ConstraintName);
    }

    private sealed class NoHomeSummaryCache : IHomeSummaryCache
    {
        public ValueTask<HomeSummaryDto?> GetAsync(int creatorId, CancellationToken ct) => ValueTask.FromResult<HomeSummaryDto?>(null);
        public Task SetAsync(int creatorId, HomeSummaryDto value, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(int creatorId, CancellationToken ct) => Task.CompletedTask;
    }
}
