using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Products.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Analytics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>
/// A partly refunded order counts net of its refund in every revenue figure — order summary and its monthly trend, the
/// period board and its buckets, the home summary (totals, trends, top product, recent orders), a page's sales, product
/// revenue, the analytics series, product and page breakdowns — and its fee keeps only its share (as the ledger's fee
/// refund). The orders list and CSV show the refund and what the order keeps. A fully refunded order stays out of revenue.
/// The refund figures count partial refunds too: the refunded amount sums RefundedCents and the count is "orders with a
/// refund", at the order's latest refund time.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PartialRefundRevenueTests
{
    private const string Zagreb = "Europe/Zagreb";
    private static readonly CancellationToken None = CancellationToken.None;

    // A: 50.00, fee 2.50, 10.00 refunded → keeps 40.00 and a 2.00 fee. B: 20.00, fee 1.00. C: fully refunded.
    private const long Revenue = 4000 + 2000;
    private const long Fees = 200 + 100;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public PartialRefundRevenueTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private sealed record Seeded(TestData.SeededCreator Creator, int ProductId, Guid ProductPublicId, int PageId, Guid PartlyRefundedOrder);

    private async Task<Seeded> SeedAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creator.CreatorId}, 'Brand', '#E2553A', {Zagreb}, 'en', now(), now())
            """);
        var productPublicId = Guid.NewGuid();
        var productId = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName",
                                           "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({productPublicId}, {creator.CreatorId}, 'Course', 5000, 'Active', 'ProductFile/t/c.zip', 'c.zip', 1024,
                    'application/zip', now(), now() - interval '400 days', now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        var pageId = (await db.Database.SqlQuery<int>($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creator.CreatorId}, {productId}, 'Sale', {$"p-{Guid.NewGuid():N}"}, 'Sales', 'Published',
                    now() - interval '400 days', now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();

        var partlyRefunded = Guid.NewGuid();
        var paidAt = DateTimeOffset.UtcNow.AddHours(-3);
        foreach (var (publicId, status, amount, fee, refunded) in new[]
                 {
                     (partlyRefunded, "Paid", 5000, 250, 1000),
                     (Guid.NewGuid(), "Paid", 2000, 100, 0),
                     (Guid.NewGuid(), "Refunded", 1500, 75, 1500),
                 })
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "LandingPageId", "Email", "AmountCents", "Currency",
                                           "Status", "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                           "RefundedCents", "CreatedAt", "PaidAt", "UpdatedAt")
                VALUES ({publicId}, {creator.CreatorId}, {productId}, {pageId}, 'buyer@example.test', {amount}, 'Eur', {status}, 500,
                        {fee}, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {refunded}, {paidAt}, {paidAt}, {paidAt.AddMinutes(30)})
                """);
        }

        return new Seeded(creator, productId, productPublicId, pageId, partlyRefunded);
    }

    [Fact]
    public async Task TheGeneratedColumns_KeepTheUnrefundedPart_AndTheFeesShareRoundedAsTheLedger()
    {
        var seeded = await SeedAsync();
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            UPDATE orders.orders SET "AmountCents" = 999, "PlatformFeeCents" = 33, "RefundedCents" = 333
            WHERE "PublicId" = {seeded.PartlyRefundedOrder}
            """);

        var kept = (await db.Set<CreatorPlatform.Orders.Domain.Orders.Order>().AsNoTracking()
            .Where(o => o.PublicId == seeded.PartlyRefundedOrder)
            .Select(o => new { o.KeptAmountCents, o.KeptPlatformFeeCents }).ToListAsync()).Single();

        Assert.Equal((666, 22), (kept.KeptAmountCents, kept.KeptPlatformFeeCents));   // the ledger gives back 11 of 33
    }

    [Fact]
    public async Task TheOrderBoards_CountThePartlyRefundedOrderNet()
    {
        var seeded = await SeedAsync();
        await using var db = _fixture.CreateDbContext();
        var orders = new OrderRepository(db);

        var summary = await orders.GetSummaryByCreatorSlugAsync(seeded.Creator.Slug, seeded.Creator.OwnerUserId, None);
        Assert.Equal((2, (int)Revenue, (int)Fees, (int)(Revenue - Fees)),
            (summary.PaidOrderCount, summary.TotalPaidAmountCents, summary.TotalPlatformFeeCents, summary.NetAmountCents));
        Assert.Equal((Revenue, Fees), (summary.MonthlyTrend.Sum(m => m.GrossCents), summary.MonthlyTrend.Sum(m => m.FeeCents)));

        var plan = AnalyticsPeriodPlan.Create("30d", DateTimeOffset.UtcNow, Zagreb, DateTimeOffset.UtcNow.AddDays(-400));
        var period = await orders.GetPeriodSummaryAsync(seeded.Creator.CreatorId, Zagreb, plan, None);
        Assert.Equal((Revenue, Fees), (period.Totals.TotalPaidAmountCents, period.Totals.TotalPlatformFeeCents));
        Assert.Equal((Revenue, Fees), (period.Buckets.Values.Sum(b => b.GrossCents), period.Buckets.Values.Sum(b => b.FeeCents)));

        var page = await orders.GetSalesByPeriodForLandingPageAsync(seeded.PageId, StatsPeriods.At(DateTimeOffset.UtcNow), None);
        Assert.Equal((Revenue, Revenue), (page.Last30Days.RevenueCents, page.AllTime.RevenueCents));

        var byPage = Assert.Single(await orders.GetOrdersSummaryByCreatorGroupedByLandingPageAsync(seeded.Creator.Slug, seeded.Creator.OwnerUserId, None));
        Assert.Equal(Revenue, byPage.TotalRevenueCents);

        var products = await new OrderContextProvider(db).GetProductRevenueByCreatorIdAsync(seeded.Creator.CreatorId, None);
        Assert.Equal((int)Revenue, products[seeded.ProductId].RevenueCents);
    }

    [Fact]
    public async Task TheHomeSummary_CountsThePartlyRefundedOrderNet()
    {
        var seeded = await SeedAsync();
        await using var db = _fixture.CreateDbContext();

        var home = await new OrderRepository(db).GetHomeSummaryByCreatorIdAsync(seeded.Creator.CreatorId, None);

        Assert.Equal(((int)Revenue, (int)Fees), (home.TotalPaidAmountCents, home.TotalPlatformFeeCents));
        Assert.Equal((int)Revenue, home.RevenueTrend.Sum());
        Assert.Equal((int)Revenue, home.MonthlyRevenueTrend.Sum());
        Assert.Equal((int)Revenue, home.TopProduct!.TotalCents);
        var recent = Assert.Single(home.RecentOrders, o => o.PublicId == seeded.PartlyRefundedOrder);
        Assert.Equal((5000, 1000, 200, 3800), (recent.AmountCents, recent.RefundedCents, recent.PlatformFeeCents, recent.NetAmountCents));
    }

    [Fact]
    public async Task TheOrdersListAndCsv_ShowTheRefund_AndWhatTheOrderKeeps()
    {
        var seeded = await SeedAsync();
        await using var db = _fixture.CreateDbContext();

        var rows = await new OrderRepository(db).GetByCreatorSlugAsync(
            seeded.Creator.Slug, seeded.Creator.OwnerUserId, new OrderListFilter(null, null, null, null, null), null, 50, None);

        var partly = Assert.Single(rows, o => o.PublicId == seeded.PartlyRefundedOrder);
        Assert.Equal(("Paid", 5000, 1000, 200, 3800), (partly.Status, partly.AmountCents, partly.RefundedCents, partly.PlatformFeeCents, partly.NetAmountCents));
        Assert.Contains(",Paid,50.00,10.00,2.00,38.00,EUR,", OrdersCsv.Row(partly));
        Assert.StartsWith("order_id,created_at,customer_name,customer_email,product,landing_page,status,amount,refunded,platform_fee,net,", OrdersCsv.Header);

        var full = Assert.Single(rows, o => o.Status == "Refunded");
        Assert.Equal((1500, 75, 1425), (full.RefundedCents, full.PlatformFeeCents, full.NetAmountCents));   // unchanged for a full refund
    }

    [Fact]
    public async Task TheAnalytics_CountThePartlyRefundedOrderNet()
    {
        var seeded = await SeedAsync();
        await using var db = _fixture.CreateDbContext();
        var analytics = new AnalyticsSeriesService(new AnalyticsSeriesReader(db));
        var (slug, owner) = (seeded.Creator.Slug, seeded.Creator.OwnerUserId);

        var workspace = await analytics.GetAsync(slug, owner, "workspace", Guid.Empty, "30d", None);
        Assert.Equal((2L, Revenue), (workspace.Totals.Sales, workspace.Totals.RevenueCents));
        Assert.Equal(Revenue, workspace.Buckets.Sum(b => b.RevenueCents));

        var product = await analytics.GetAsync(slug, owner, "product", seeded.ProductPublicId, "30d", None);
        Assert.Equal((Revenue, Revenue), (product.Totals.RevenueCents, product.Product!.CreatorRevenueCents));

        var breakdown = await analytics.GetProductBreakdownAsync(slug, owner, "30d", null, None);
        Assert.Equal((Revenue, Revenue), (breakdown.TotalRevenueCents, Assert.Single(breakdown.Products).RevenueCents));

        var pages = await analytics.GetTopPagesAsync(slug, owner, "30d", null, null, None);
        Assert.Equal(Revenue, Assert.Single(pages.Pages).RevenueCents);
    }

    // ---- Refund figures: full and partial refunds ------------------------------------------------------------------

    private const long RefundedCents = 1000 + 1500;   // A's partial refund and C's full one

    [Fact]
    public async Task TheRefundFigures_CountPartialRefunds_AsOrdersWithARefund()
    {
        var seeded = await SeedAsync();
        await using var db = _fixture.CreateDbContext();
        var orders = new OrderRepository(db);

        var summary = await orders.GetSummaryByCreatorSlugAsync(seeded.Creator.Slug, seeded.Creator.OwnerUserId, None);
        Assert.Equal((2, (int)RefundedCents), (summary.RefundedOrderCount, summary.RefundedAmountCents));
        Assert.Equal(2, summary.MonthlyTrend.Sum(m => m.RefundedCount));

        var plan = AnalyticsPeriodPlan.Create("30d", DateTimeOffset.UtcNow, Zagreb, DateTimeOffset.UtcNow.AddDays(-400));
        var period = await orders.GetPeriodSummaryAsync(seeded.Creator.CreatorId, Zagreb, plan, None);
        Assert.Equal((2, RefundedCents), (period.Totals.RefundedOrderCount, period.Totals.RefundedAmountCents));
        Assert.Equal((2, 0, 0), (period.Totals.PaidOrderCount, period.Totals.PendingOrderCount, period.Totals.FailedOrderCount));
        Assert.Equal((2, RefundedCents), (period.Buckets.Values.Sum(b => b.RefundedCount), period.Buckets.Values.Sum(b => b.RefundedCents)));

        var home = await orders.GetHomeSummaryByCreatorIdAsync(seeded.Creator.CreatorId, None);
        Assert.Equal((2, (int)RefundedCents), (home.RefundedOrderCount, home.RefundedAmountCents));

        var rows = await orders.GetByCreatorSlugAsync(
            seeded.Creator.Slug, seeded.Creator.OwnerUserId, new OrderListFilter(null, null, null, null, null), null, 50, None);
        var partly = Assert.Single(rows, o => o.PublicId == seeded.PartlyRefundedOrder);
        Assert.NotNull(partly.RefundedAt);
        Assert.Null(Assert.Single(rows, o => o.Status == "Paid" && o.RefundedCents == 0).RefundedAt);
    }

    [Fact]
    public async Task TheAnalytics_CountPartialRefunds()
    {
        var seeded = await SeedAsync();
        await using var db = _fixture.CreateDbContext();
        var analytics = new AnalyticsSeriesService(new AnalyticsSeriesReader(db));
        var (slug, owner) = (seeded.Creator.Slug, seeded.Creator.OwnerUserId);

        var workspace = await analytics.GetAsync(slug, owner, "workspace", Guid.Empty, "30d", None);
        Assert.Equal((2, RefundedCents), (workspace.Extras!.RefundCount, workspace.Extras.RefundedCents));

        var product = await analytics.GetAsync(slug, owner, "product", seeded.ProductPublicId, "30d", None);
        Assert.Equal((2, RefundedCents), (product.Product!.RefundCount, product.Product.RefundedCents));
        Assert.Equal((2, RefundedCents), (product.Extras!.RefundCount, product.Extras.RefundedCents));
    }

    [Fact]
    public async Task APartialRefund_CountsAtTheRefundTime_NotWhenTheOrderWasPaid()
    {
        var seeded = await SeedAsync();
        var paidAt = DateTimeOffset.UtcNow.AddDays(-40);
        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""
                UPDATE orders.orders SET "CreatedAt" = {paidAt}, "PaidAt" = {paidAt}, "UpdatedAt" = now() - interval '3 hours'
                WHERE "PublicId" = {seeded.PartlyRefundedOrder}
                """);

        await using var read = _fixture.CreateDbContext();
        var plan = AnalyticsPeriodPlan.Create("30d", DateTimeOffset.UtcNow, Zagreb, DateTimeOffset.UtcNow.AddDays(-400));
        var period = await new OrderRepository(read).GetPeriodSummaryAsync(seeded.Creator.CreatorId, Zagreb, plan, None);

        // Paid 40 days ago: its revenue is outside the 30 days; its refund (3 hours ago) is inside.
        Assert.Equal((1, 2000L), (period.Totals.PaidOrderCount, period.Totals.TotalPaidAmountCents));
        Assert.Equal((2, RefundedCents), (period.Totals.RefundedOrderCount, period.Totals.RefundedAmountCents));
    }

    [Fact]
    public async Task TheDatabase_RefusesARefundedOrderNotRefundedInFull_AndARefundOnAnUnpaidOrder()
    {
        var seeded = await SeedAsync();
        await using var db = _fixture.CreateDbContext();

        var partialRefunded = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            UPDATE orders.orders SET "RefundedCents" = 100 WHERE "CreatorId" = {seeded.Creator.CreatorId} AND "Status" = 'Refunded'
            """));
        Assert.Equal("CK_orders_RefundedCents_MatchStatus", partialRefunded.ConstraintName);

        var pending = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            UPDATE orders.orders SET "Status" = 'Pending' WHERE "PublicId" = {seeded.PartlyRefundedOrder}
            """));
        Assert.Equal("CK_orders_RefundedCents_MatchStatus", pending.ConstraintName);
    }
}
