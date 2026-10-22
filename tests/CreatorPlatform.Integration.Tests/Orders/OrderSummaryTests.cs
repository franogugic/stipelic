using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Shared.Application.Analytics;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>The orders summary board: totals per status, refunded amount, and 12 months in the creator's timezone.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderSummaryTests
{
    private const string Zagreb = "Europe/Zagreb";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public OrderSummaryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<OrderSummaryDto> SummaryAsync(TestData.SeededCreator creator)
    {
        await using var db = _fixture.CreateDbContext();
        return await new OrderRepository(db).GetSummaryByCreatorSlugAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None);
    }

    [Fact]
    public async Task CountsEveryStatus_AndTheRefundedAmount()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId);
        var now = DateTimeOffset.UtcNow;
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 2900, 290, now, now);
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 1000, 100, now, now);
        await AddOrderAsync(creator.CreatorId, productId, "Pending", 1000, 100, null, now);
        await AddOrderAsync(creator.CreatorId, productId, "Failed", 1000, 100, null, now);
        await AddOrderAsync(creator.CreatorId, productId, "Refunded", 1500, 150, now, now);

        var summary = await SummaryAsync(creator);

        Assert.Equal((2, 1, 1, 1, 5), (summary.PaidOrderCount, summary.PendingOrderCount, summary.FailedOrderCount, summary.RefundedOrderCount, summary.TotalOrderCount));
        Assert.Equal((3900, 390, 3510, 1500), (summary.TotalPaidAmountCents, summary.TotalPlatformFeeCents, summary.NetAmountCents, summary.RefundedAmountCents));
    }

    [Fact]
    public async Task TheTrend_HasTwelveCreatorTimezoneMonths_OldestFirst()
    {
        var creator = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        var productId = await AddProductAsync(creator.CreatorId);
        var now = DateTimeOffset.UtcNow;
        var utcPreviousMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1);
        // 23:30 UTC on the last day of a UTC month is already the 1st of the next month in Zagreb.
        var lateNight = utcPreviousMonth.AddMinutes(-30);
        var localMonth = TimeZoneInfo.ConvertTime(lateNight, TimeZoneInfo.FindSystemTimeZoneById(Zagreb)).ToString("yyyy-MM");
        var utcMonth = lateNight.ToString("yyyy-MM");

        await AddOrderAsync(creator.CreatorId, productId, "Paid", 2000, 200, lateNight, lateNight);
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 500, 50, now, now);
        // Refunded this month, bought last year: counted when it was refunded, and never in gross.
        await AddOrderAsync(creator.CreatorId, productId, "Refunded", 900, 90, now.AddYears(-1), now.AddYears(-1), updatedAt: now);

        var trend = (await SummaryAsync(creator)).MonthlyTrend;

        Assert.Equal(12, trend.Count);
        Assert.Equal(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(Zagreb)).ToString("yyyy-MM"), trend[^1].Month);
        var boundary = trend.Single(m => m.Month == localMonth);
        Assert.Equal((2000L, 200L, 1800L), (boundary.GrossCents, boundary.FeeCents, boundary.NetCents));
        Assert.NotEqual(utcMonth, localMonth);
        Assert.DoesNotContain(trend, m => m.Month == utcMonth && m.GrossCents > 0);
        Assert.Equal((500L, 1), (trend[^1].GrossCents, trend[^1].RefundedCount));
        Assert.Equal(2500L, trend.Sum(m => m.GrossCents));
    }

    [Fact]
    public async Task AnotherOwnersSlug_AnswersZeros()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId);
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 1000, 100, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        await using var db = _fixture.CreateDbContext();
        var summary = await new OrderRepository(db).GetSummaryByCreatorSlugAsync(creator.Slug, other.OwnerUserId, CancellationToken.None);

        Assert.Equal((0, 0), (summary.TotalOrderCount, summary.MonthlyTrend.Count));
    }

    private async Task<(OrderPeriodContext Context, OrderPeriodRead Read, AnalyticsPeriodPlan Plan)> PeriodAsync(TestData.SeededCreator creator, string period)
    {
        await using var db = _fixture.CreateDbContext();
        var repository = new OrderRepository(db);
        var context = (await repository.GetPeriodContextAsync(creator.Slug, creator.OwnerUserId, CancellationToken.None))!;
        var plan = AnalyticsPeriodPlan.Create(period, DateTimeOffset.UtcNow, context.Timezone, context.CreatedAt);
        return (context, await repository.GetPeriodSummaryAsync(context.CreatorId, context.Timezone, plan, CancellationToken.None), plan);
    }

    private static DateTimeOffset LocalNoon(int days)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date.AddDays(days).AddHours(12);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    [Fact]
    public async Task APeriod_CountsOnlyItsOwnOrders_PaidByPaidAt_RefundsByRefundTime_OthersByCreation()
    {
        var creator = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        var productId = await AddProductAsync(creator.CreatorId);
        var other = await _data.CreateCreatorAsync();
        var otherProductId = await AddProductAsync(other.CreatorId);

        await AddOrderAsync(creator.CreatorId, productId, "Paid", 2900, 290, LocalNoon(-2), LocalNoon(-3), LocalNoon(-2));   // created earlier, paid in the 7 days
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 1000, 100, LocalNoon(0), LocalNoon(0));
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 5000, 500, LocalNoon(-20), LocalNoon(-20));                // outside 7d
        await AddOrderAsync(creator.CreatorId, productId, "Refunded", 1500, 150, LocalNoon(-10), LocalNoon(-10), LocalNoon(-1)); // refunded inside 7d
        await AddOrderAsync(creator.CreatorId, productId, "Refunded", 800, 80, LocalNoon(-30), LocalNoon(-30), LocalNoon(-9));    // refunded before
        await AddOrderAsync(creator.CreatorId, productId, "Pending", 1000, 100, null, LocalNoon(-1));
        await AddOrderAsync(creator.CreatorId, productId, "Pending", 1000, 100, null, LocalNoon(-12));
        await AddOrderAsync(creator.CreatorId, productId, "Failed", 1000, 100, null, LocalNoon(0));
        await AddOrderAsync(other.CreatorId, otherProductId, "Paid", 7777, 777, LocalNoon(0), LocalNoon(0));

        var (context, read, plan) = await PeriodAsync(creator, "7d");

        Assert.Equal("Eur", context.Currency);
        var totals = read.Totals;
        Assert.Equal((2, 3900L, 390L), (totals.PaidOrderCount, totals.TotalPaidAmountCents, totals.TotalPlatformFeeCents));
        Assert.Equal((1, 1500L), (totals.RefundedOrderCount, totals.RefundedAmountCents));
        Assert.Equal((1, 1), (totals.PendingOrderCount, totals.FailedOrderCount));

        // Buckets: gross where paid, the refund where it happened, zero elsewhere.
        Assert.Equal(7, plan.Buckets.Count);
        Assert.Equal(3900L, read.Buckets.Values.Sum(b => b.GrossCents));
        Assert.Equal(390L, read.Buckets.Values.Sum(b => b.FeeCents));
        Assert.Equal(1, read.Buckets.Values.Sum(b => b.RefundedCount));
        Assert.Equal(1000L, read.Buckets[plan.Buckets[^1]].GrossCents);
        Assert.Equal(2900L, read.Buckets[plan.Buckets[^3]].GrossCents);
        Assert.Equal(1, read.Buckets[plan.Buckets[^2]].RefundedCount);
    }

    [Fact]
    public async Task AllTime_TotalsCoverEverything_TheBucketsTheLastTwelveMonths()
    {
        var creator = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        var productId = await AddProductAsync(creator.CreatorId);
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 1000, 100, LocalNoon(0), LocalNoon(0));
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 4000, 400, LocalNoon(-400), LocalNoon(-400)); // before the chart

        var (_, read, plan) = await PeriodAsync(creator, "all");

        Assert.Equal((2, 5000L), (read.Totals.PaidOrderCount, read.Totals.TotalPaidAmountCents));
        Assert.True(plan.Buckets.Count <= 12);
        Assert.Equal(1000L, read.Buckets.Values.Sum(b => b.GrossCents));
    }

    [Fact]
    public async Task ALongPeriod_IsBucketedInWeeks_AndAnEventLandsInTheWeekItBelongsTo()
    {
        var creator = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        var productId = await AddProductAsync(creator.CreatorId);
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 1000, 100, LocalNoon(-59), LocalNoon(-59)); // the window's first day
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 2000, 200, LocalNoon(-53), LocalNoon(-53)); // its seventh day: still week 1
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 3000, 300, LocalNoon(-52), LocalNoon(-52)); // the eighth: week 2
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 9000, 900, LocalNoon(-61), LocalNoon(-61)); // before the window

        var (_, read, plan) = await PeriodAsync(creator, "2m");

        Assert.Equal(("week", 9), (plan.Step, plan.Buckets.Count));
        Assert.Equal(3000L, read.Buckets[plan.Buckets[0]].GrossCents);
        Assert.Equal(3000L, read.Buckets[plan.Buckets[1]].GrossCents);
        Assert.Equal(2, read.Buckets.Count);
        Assert.Equal((3, 6000L), (read.Totals.PaidOrderCount, read.Totals.TotalPaidAmountCents));
    }

    [Fact]
    public async Task APeriodSummary_OfAnotherOwnersWorkspace_HasNoContext()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();

        await using var db = _fixture.CreateDbContext();
        var context = await new OrderRepository(db).GetPeriodContextAsync(creator.Slug, other.OwnerUserId, CancellationToken.None);

        Assert.Null(context);
    }

    private async Task SetTimezoneAsync(int creatorId, string timezone)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, 'Brand', '#E2553A', {timezone}, 'en', {now}, {now})
            """);
    }

    private async Task<int> AddProductAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Product', 1000, 'Digital', 'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task AddOrderAsync(
        int creatorId, int productId, string status, int amountCents, int feeCents, DateTimeOffset? paidAt, DateTimeOffset createdAt,
        DateTimeOffset? updatedAt = null)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {TestData.UniqueEmail("buyer")}, {amountCents}, 'Eur', {status},
                    1000, {feeCents}, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {createdAt}, {paidAt}, {updatedAt ?? createdAt})
            """);
    }
}
