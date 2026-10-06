using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Analytics;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>Page analytics per period: views, sales and emails are cut at the same, inclusive lower bounds.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PageAnalyticsPeriodTests
{
    // A fixed "now" keeps the boundaries deterministic: today from 15 Jun 00:00, 7 days from 8 Jun 12:00,
    // 30 days from 16 May 12:00 (all UTC).
    private static readonly StatsPeriods Periods = StatsPeriods.At(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    private static readonly TimeSpan JustBefore = TimeSpan.FromTicks(10); // 1 µs, Postgres' resolution

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public PageAnalyticsPeriodTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    /// <summary>One event exactly on each lower bound and one just before it, plus an older one.</summary>
    private static readonly DateTimeOffset[] Instants =
    [
        Periods.StartOfToday,                       // today, 7d, 30d, all
        Periods.StartOfToday - JustBefore,          // 7d, 30d, all
        Periods.Last7DaysFrom,                      // 7d, 30d, all
        Periods.Last7DaysFrom - JustBefore,         // 30d, all
        Periods.Last30DaysFrom,                     // 30d, all
        Periods.Last30DaysFrom - JustBefore,        // all
    ];

    [Fact]
    public async Task Sales_AreCountedByPaidAt_WithInclusiveBounds_AndOnlyWhilePaid()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var otherPage = await _data.CreateLandingPageAsync(creator.CreatorId);
        var productId = await CreateProductAsync(creator.CreatorId);
        for (var i = 0; i < Instants.Length; i++)
            await InsertOrderAsync(creator.CreatorId, productId, page, 1000 << i, "Paid", Instants[i]);
        await InsertOrderAsync(creator.CreatorId, productId, page, 64_000, "Refunded", Periods.StartOfToday);
        await InsertOrderAsync(creator.CreatorId, productId, page, 128_000, "Pending", null);
        await InsertOrderAsync(creator.CreatorId, productId, otherPage, 256_000, "Paid", Periods.StartOfToday);

        await using var db = _fixture.CreateDbContext();
        var sales = await new OrderRepository(db).GetSalesByPeriodForLandingPageAsync(page, Periods, CancellationToken.None);

        Assert.Equal((1, 1_000L), (sales.Today.PurchaseCount, sales.Today.RevenueCents));
        Assert.Equal((3, 7_000L), (sales.Last7Days.PurchaseCount, sales.Last7Days.RevenueCents));
        Assert.Equal((5, 31_000L), (sales.Last30Days.PurchaseCount, sales.Last30Days.RevenueCents));
        Assert.Equal((6, 63_000L), (sales.AllTime.PurchaseCount, sales.AllTime.RevenueCents));
        Assert.Equal("Eur", sales.Currency);
    }

    [Fact]
    public async Task Captures_AreCountedByCapturedAt_WithInclusiveBounds()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var otherPage = await _data.CreateLandingPageAsync(creator.CreatorId);
        foreach (var instant in Instants)
            await InsertCaptureAsync(page, instant);
        await InsertCaptureAsync(otherPage, Periods.StartOfToday);

        await using var db = _fixture.CreateDbContext();
        var captures = await new EmailCaptureRepository(db).GetCaptureCountsByPeriodAsync(page, Periods, CancellationToken.None);

        Assert.Equal((1L, 3L, 5L, 6L), (captures.Today, captures.Last7Days, captures.Last30Days, captures.AllTime));
    }

    [Fact]
    public async Task Views_UseTheSameBounds()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        foreach (var instant in Instants)
            await InsertViewAsync(page, instant);

        await using var db = _fixture.CreateDbContext();
        var views = await new PageViewRepository(db).GetStatsAsync(page, Periods, CancellationToken.None);

        Assert.Equal((1L, 3L, 5L, 6L), (views.ViewsToday, views.ViewsLast7Days, views.ViewsLast30Days, views.TotalViews));
    }

    [Fact]
    public async Task APageWithoutData_IsAllZero()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);

        await using var db = _fixture.CreateDbContext();
        var sales = await new OrderRepository(db).GetSalesByPeriodForLandingPageAsync(page, Periods, CancellationToken.None);
        var captures = await new EmailCaptureRepository(db).GetCaptureCountsByPeriodAsync(page, Periods, CancellationToken.None);

        Assert.Equal((0, 0L), (sales.AllTime.PurchaseCount, sales.AllTime.RevenueCents));
        Assert.Null(sales.Currency);
        Assert.Equal(0, captures.AllTime);
    }

    [Fact]
    public async Task TimeSeriesPurchases_AreBucketedByPaidAt_NotCreatedAt()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var productId = await CreateProductAsync(creator.CreatorId);
        // Created on one day, paid exactly a day later: the sale belongs to the later bucket, as on the period cards.
        var createdAt = DateTimeOffset.UtcNow.AddDays(-3);
        var paidAt = createdAt.AddDays(1);
        await InsertOrderAsync(creator.CreatorId, productId, page, 2900, "Paid", paidAt, createdAt);

        await using var db = _fixture.CreateDbContext();
        var rows = await new OrderRepository(db).GetBucketedPurchasesAsync(
            page, DateTimeOffset.UtcNow.AddDays(-7), "day", CancellationToken.None);

        var hit = Assert.Single(rows, r => r.PurchaseCount > 0);
        Assert.Equal((1, 2900), (hit.PurchaseCount, hit.RevenueCents));
        Assert.True(hit.BucketStart > createdAt, "the sale must not be counted in the bucket of its creation day");
        Assert.True(hit.BucketStart <= paidAt);
    }

    private async Task<int> CreateProductAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Test product', 1000, 'Digital', 'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task InsertOrderAsync(
        int creatorId, int productId, int landingPageId, int amountCents, string status, DateTimeOffset? paidAt,
        DateTimeOffset? createdAtOverride = null)
    {
        await using var db = _fixture.CreateDbContext();
        var createdAt = createdAtOverride ?? paidAt ?? Periods.StartOfToday;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "LandingPageId", "Email", "AmountCents", "Currency",
                                       "Status", "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode",
                                       "StripeCheckoutSessionId", "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {landingPageId}, {TestData.UniqueEmail("buyer")}, {amountCents},
                    'Eur', {status}, 0, 0, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {createdAt}, {paidAt}, {createdAt})
            """);
    }

    private async Task InsertCaptureAsync(int landingPageId, DateTimeOffset capturedAt)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.email_captures ("Id", "LandingPageId", "Email", "CapturedAt")
            VALUES ({Guid.NewGuid()}, {landingPageId}, {TestData.UniqueEmail()}, {capturedAt})
            """);
    }

    private async Task InsertViewAsync(int landingPageId, DateTimeOffset viewedAt)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.page_views ("Id", "LandingPageId", "VisitorId", "ViewedAt", "ViewedDate")
            VALUES ({Guid.NewGuid()}, {landingPageId}, {Guid.NewGuid()}, {viewedAt}, {DateOnly.FromDateTime(viewedAt.UtcDateTime)})
            """);
    }
}
