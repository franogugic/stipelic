using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>The dashboard's "sales at a glance" figures: customers, refunds and paid orders per local month.</summary>
[Collection(PostgresCollection.Name)]
public sealed class HomeSummaryBoardTests
{
    private const string Zagreb = "Europe/Zagreb";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public HomeSummaryBoardTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    [Fact]
    public async Task CustomersRefundsAndMonthlyPaidOrders()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        await SetTimezoneAsync(creator.CreatorId, Zagreb);
        var productId = await AddProductAsync(creator.CreatorId);
        var otherProductId = await AddProductAsync(other.CreatorId);
        var now = DateTimeOffset.UtcNow;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Zagreb);
        // 23:30 UTC on the last day of a UTC month is the 1st of the next month in Zagreb.
        var lateNight = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-1).AddMinutes(-30);
        var localMonths = Enumerable.Range(0, 12)
            .Select(i => TimeZoneInfo.ConvertTime(now, zone).AddMonths(i - 11).ToString("yyyy-MM"))
            .ToList();

        await AddOrderAsync(creator.CreatorId, productId, "Paid", 1000, "ana@example.test", now);
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 1000, "ANA@example.test", now); // same buyer
        await AddOrderAsync(creator.CreatorId, productId, "Paid", 1000, "marko@example.test", lateNight);
        await AddOrderAsync(creator.CreatorId, productId, "Refunded", 2500, "iva@example.test", now);
        await AddOrderAsync(creator.CreatorId, productId, "Pending", 1000, "luka@example.test", now);
        await AddOrderAsync(other.CreatorId, otherProductId, "Paid", 1000, "someone@example.test", now);

        await using var db = _fixture.CreateDbContext();
        var summary = await new OrderRepository(db).GetHomeSummaryByCreatorIdAsync(creator.CreatorId, CancellationToken.None);

        Assert.Equal((2, 1, 2500), (summary.CustomerCount, summary.RefundedOrderCount, summary.RefundedAmountCents));
        Assert.Equal(12, summary.MonthlyPaidOrderCounts.Count);
        Assert.Equal(2, summary.MonthlyPaidOrderCounts[^1]);
        var localLateMonth = TimeZoneInfo.ConvertTime(lateNight, zone).ToString("yyyy-MM");
        Assert.Equal(1, summary.MonthlyPaidOrderCounts[localMonths.IndexOf(localLateMonth)]);
        Assert.Equal(3, summary.MonthlyPaidOrderCounts.Sum());
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

    private async Task AddOrderAsync(int creatorId, int productId, string status, int amountCents, string email, DateTimeOffset at)
    {
        await using var db = _fixture.CreateDbContext();
        DateTimeOffset? paidAt = status is "Paid" or "Refunded" ? at : null;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {email}, {amountCents}, 'Eur', {status},
                    1000, 100, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {at}, {paidAt}, {at})
            """);
    }
}
