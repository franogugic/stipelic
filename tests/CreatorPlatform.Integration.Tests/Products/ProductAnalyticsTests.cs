using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Services;
using CreatorPlatform.Products.Infrastructure.Persistence;
using CreatorPlatform.Products.Infrastructure.Repositories;
using CreatorPlatform.Products.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Products;

/// <summary>Product analytics against a real Postgres: the numbers, the contact union, the selling pages, ownership,
/// and the published-page guard that stops an Active product from going back to Draft.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ProductAnalyticsTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public ProductAnalyticsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private static ProductService CreateService(CreatorPlatformDbContext db) => new(
        new ProductRepository(db),
        new CreatorContextProvider(db),
        new ProductsUnitOfWork(db),
        new OrderContextProvider(db),
        new LandingPageContextProvider(db),
        new AnalyticsContextProvider(db));

    private static DateTimeOffset MonthStart()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
    }

    // --- numbers ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Numbers_CountPaidOrdersOfThisProduct_ByPaidDate()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);
        var other = await CreateProductAsync(creator.CreatorId);
        var monthStart = MonthStart();
        var now = DateTimeOffset.UtcNow;

        await InsertOrderAsync(creator.CreatorId, product.Id, 1_000, "Paid", paidAt: now, createdAt: now);
        // Created last month but paid this month: it belongs to this month.
        await InsertOrderAsync(creator.CreatorId, product.Id, 2_000, "Paid", paidAt: now.AddMinutes(-1), createdAt: monthStart.AddDays(-3));
        await InsertOrderAsync(creator.CreatorId, product.Id, 4_000, "Paid", paidAt: monthStart.AddDays(-5), createdAt: monthStart.AddDays(-5));
        await InsertOrderAsync(creator.CreatorId, product.Id, 8_000, "Paid", paidAt: monthStart.AddMonths(-3).AddDays(2), createdAt: monthStart.AddMonths(-3).AddDays(2));
        // An order from before PaidAt was recorded falls back to CreatedAt.
        await InsertOrderAsync(creator.CreatorId, product.Id, 16_000, "Paid", paidAt: null, createdAt: monthStart.AddMonths(-2).AddDays(4));
        await InsertOrderAsync(creator.CreatorId, product.Id, 32_000, "Refunded", paidAt: now, createdAt: now);
        await InsertOrderAsync(creator.CreatorId, product.Id, 64_000, "Pending", paidAt: null, createdAt: now);
        await InsertOrderAsync(creator.CreatorId, other.Id, 128_000, "Paid", paidAt: now, createdAt: now);

        await using var db = _fixture.CreateDbContext();
        var result = await CreateService(db).GetAnalyticsAsync(creator.Slug, product.PublicId, creator.OwnerUserId, "1y", CancellationToken.None);

        Assert.Equal(31_000, result.RevenueCents);
        Assert.Equal(5, result.SalesCount);
        Assert.Equal(3_000, result.ThisMonthRevenueCents);
        Assert.Equal("month", result.Granularity);
        Assert.Equal(12, result.Points.Count);
        Assert.Equal(31_000, result.Points.Sum(p => p.RevenueCents));
        Assert.Equal(MonthStart().ToString("yyyy-MM-dd"), result.Points[^1].BucketStart);
        Assert.Equal(3_000, result.Points[^1].RevenueCents);
        Assert.Equal(4_000, result.Points[^2].RevenueCents);
        Assert.Equal(16_000, result.Points[^3].RevenueCents);
        Assert.Equal(8_000, result.Points[^4].RevenueCents);
        Assert.Equal(0, result.Points[0].RevenueCents);
    }

    [Fact]
    public async Task EveryRange_ReturnsEveryBucket_AndTheTotalsDoNotDependOnIt()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);
        var now = DateTimeOffset.UtcNow;
        await InsertOrderAsync(creator.CreatorId, product.Id, 2_900, "Paid", paidAt: now, createdAt: now);
        await InsertOrderAsync(creator.CreatorId, product.Id, 5_000, "Paid", paidAt: now.AddYears(-3), createdAt: now.AddYears(-3));

        await using var db = _fixture.CreateDbContext();
        var service = CreateService(db);

        foreach (var (range, buckets, granularity) in new[] { ("30d", 30, "day"), ("3m", 3, "month"), ("6m", 6, "month"), ("1y", 12, "month") })
        {
            var result = await service.GetAnalyticsAsync(creator.Slug, product.PublicId, creator.OwnerUserId, range, CancellationToken.None);

            Assert.Equal(buckets, result.Points.Count);
            Assert.Equal(granularity, result.Granularity);
            Assert.Equal(7_900, result.RevenueCents); // all time, whatever the chart shows
            Assert.Equal(2, result.SalesCount);
            Assert.Equal(2_900, result.Points[^1].RevenueCents);
        }
    }

    [Fact]
    public async Task AProductWithoutData_IsAllZero_WithEmptyBuckets()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);

        await using var db = _fixture.CreateDbContext();
        var result = await CreateService(db).GetAnalyticsAsync(creator.Slug, product.PublicId, creator.OwnerUserId, null, CancellationToken.None);

        Assert.Equal((0L, 0, 0L, 0), (result.RevenueCents, result.SalesCount, result.ThisMonthRevenueCents, result.ContactCount));
        Assert.Equal(12, result.Points.Count); // the default range is 1y
        Assert.All(result.Points, p => Assert.Equal(0, p.RevenueCents));
        Assert.Empty(result.SellingPages);
    }

    // --- contacts and selling pages -------------------------------------------------------------------------------

    [Fact]
    public async Task Contacts_AreTheDistinctUnionOfPageCapturesAndDirectCaptures()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);
        var otherProduct = await CreateProductAsync(creator.CreatorId);
        var page = await CreatePageAsync(creator.CreatorId, product.Id, "Published");
        var secondPage = await CreatePageAsync(creator.CreatorId, product.Id, "Draft");
        var unrelatedPage = await CreatePageAsync(creator.CreatorId, otherProduct.Id, "Published");

        var shared = TestData.UniqueEmail("shared");
        await InsertCaptureAsync(page, null, shared);
        await InsertCaptureAsync(secondPage, null, shared);                       // the same person on two pages
        await InsertCaptureAsync(page, null, TestData.UniqueEmail("a"));
        await InsertCaptureAsync(unrelatedPage, product.Id, shared);              // tagged with the product directly: still the same person
        await InsertCaptureAsync(unrelatedPage, product.Id, TestData.UniqueEmail("direct"));
        await InsertCaptureAsync(unrelatedPage, otherProduct.Id, TestData.UniqueEmail("other"));
        var unsubscribed = TestData.UniqueEmail("gone");
        await InsertCaptureAsync(page, null, unsubscribed);
        await _data.UnsubscribeAsync(creator.CreatorId, unsubscribed);            // still a contact

        await using var db = _fixture.CreateDbContext();
        var result = await CreateService(db).GetAnalyticsAsync(creator.Slug, product.PublicId, creator.OwnerUserId, "1y", CancellationToken.None);

        Assert.Equal(4, result.ContactCount); // shared, a, direct, unsubscribed
    }

    [Fact]
    public async Task SellingPages_AreTheProductsLivePages_PublishedFirst_WithoutArchivedOnes()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);
        var other = await CreateProductAsync(creator.CreatorId);
        var draft = await CreatePageAsync(creator.CreatorId, product.Id, "Draft", "A draft");
        var published = await CreatePageAsync(creator.CreatorId, product.Id, "Published", "Z published");
        await CreatePageAsync(creator.CreatorId, product.Id, "Archived", "Old");
        await CreatePageAsync(creator.CreatorId, other.Id, "Published", "Not mine");

        await using var db = _fixture.CreateDbContext();
        var result = await CreateService(db).GetAnalyticsAsync(creator.Slug, product.PublicId, creator.OwnerUserId, "1y", CancellationToken.None);

        Assert.Equal(["Z published", "A draft"], result.SellingPages.Select(p => p.Title));
        Assert.Equal(["Published", "Draft"], result.SellingPages.Select(p => p.Status));
        Assert.Equal([published.PublicId, draft.PublicId], result.SellingPages.Select(p => p.PublicId));
    }

    // --- ownership and input ---------------------------------------------------------------------------------------

    [Fact]
    public async Task AnotherCreatorsProduct_IsNotFound_AndSoIsAnUnknownOne()
    {
        var owner = await _data.CreateCreatorAsync();
        var stranger = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(owner.CreatorId);

        await using var db = _fixture.CreateDbContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAnalyticsAsync(stranger.Slug, product.PublicId, stranger.OwnerUserId, "1y", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAnalyticsAsync(owner.Slug, Guid.NewGuid(), owner.OwnerUserId, "1y", CancellationToken.None));
        // Someone else's workspace address is as unknown as the product.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAnalyticsAsync(owner.Slug, product.PublicId, stranger.OwnerUserId, "1y", CancellationToken.None));
    }

    [Fact]
    public async Task AnInvalidRange_IsABadRequest_AndAnArchivedProductKeepsItsHistory()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId, "Archived");

        await using var db = _fixture.CreateDbContext();
        var service = CreateService(db);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.GetAnalyticsAsync(creator.Slug, product.PublicId, creator.OwnerUserId, "2y", CancellationToken.None));
        var result = await service.GetAnalyticsAsync(creator.Slug, product.PublicId, creator.OwnerUserId, "6m", CancellationToken.None);
        Assert.Equal(6, result.Points.Count);
    }

    // --- the published-page guard (Screen 14) -----------------------------------------------------------------

    [Fact]
    public async Task LandingPageContextProvider_OnlyAPublishedPageUsesTheProduct()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);
        await CreatePageAsync(creator.CreatorId, product.Id, "Draft");
        await CreatePageAsync(creator.CreatorId, product.Id, "Archived");

        await using (var db = _fixture.CreateDbContext())
            Assert.False(await new LandingPageContextProvider(db).IsUsedByPublishedPageAsync(product.Id, CancellationToken.None));

        await CreatePageAsync(creator.CreatorId, product.Id, "Published");

        await using (var db = _fixture.CreateDbContext())
            Assert.True(await new LandingPageContextProvider(db).IsUsedByPublishedPageAsync(product.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ActiveToDraft_IsBlockedByAPublishedPage_ButAllowedWithADraftPage()
    {
        var creator = await _data.CreateCreatorAsync();
        var blocked = await CreateProductAsync(creator.CreatorId);
        var allowed = await CreateProductAsync(creator.CreatorId);
        await CreatePageAsync(creator.CreatorId, blocked.Id, "Published");
        await CreatePageAsync(creator.CreatorId, allowed.Id, "Draft");

        await using var db = _fixture.CreateDbContext();
        var service = CreateService(db);
        UpdateProductRequestDto Draft(string name) => new() { Name = name, PriceCents = 1000, Type = "Digital", Status = "Draft" };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(creator.Slug, blocked.PublicId, creator.OwnerUserId, Draft("Blocked"), CancellationToken.None));
        Assert.Equal("This product is used by a published page.", ex.Message);

        var result = await service.UpdateAsync(creator.Slug, allowed.PublicId, creator.OwnerUserId, Draft("Allowed"), CancellationToken.None);
        Assert.Equal("Draft", result.Status);
    }

    // --- seeding -------------------------------------------------------------------------------------------

    private sealed record SeededProduct(int Id, Guid PublicId);

    private sealed record SeededPage(int Id, Guid PublicId);

    private async Task<SeededProduct> CreateProductAsync(int creatorId, string status = "Active")
    {
        await using var db = _fixture.CreateDbContext();
        var publicId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, {$"Product {publicId:N}"[..20]}, 1000, 'Digital', {status}, {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return new SeededProduct(id, publicId);
    }

    private async Task<SeededPage> CreatePageAsync(int creatorId, int productId, string status, string title = "Test Page")
    {
        await using var db = _fixture.CreateDbContext();
        var publicId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status",
                                                     "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, {productId}, {title}, {$"p-{Guid.NewGuid():N}"}, 'Sales', {status}, {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return new SeededPage(id, publicId);
    }

    private async Task InsertOrderAsync(
        int creatorId, int productId, int amountCents, string status, DateTimeOffset? paidAt, DateTimeOffset createdAt)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency",
                                       "Status", "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode",
                                       "StripeCheckoutSessionId", "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {TestData.UniqueEmail("buyer")}, {amountCents},
                    'Eur', {status}, 0, 0, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {createdAt}, {paidAt}, {createdAt})
            """);
    }

    private async Task InsertCaptureAsync(SeededPage page, int? productId, string email)
        => await InsertCaptureAsync(page.Id, productId, email);

    private async Task InsertCaptureAsync(int landingPageId, int? productId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.email_captures ("Id", "LandingPageId", "ProductId", "Email", "CapturedAt")
            VALUES ({Guid.NewGuid()}, {landingPageId}, {productId}, {email}, {DateTimeOffset.UtcNow})
            """);
    }
}
