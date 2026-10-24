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

/// <summary>Product rules against a real Postgres: the published-page guard that stops an Active product from going
/// back to Draft, and the six-month sales trend on the list.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ProductPublishGuardAndTrendTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public ProductPublishGuardAndTrendTests(PostgresFixture fixture)
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
        new ProductFiles(db, new InMemoryProductFileStorage()), new AnyImageAccepted());

    private static DateTimeOffset MonthStart()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
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
        UpdateProductRequestDto Draft(string name) => new() { Name = name, PriceCents = 1000, Status = "Draft" };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(creator.Slug, blocked.PublicId, creator.OwnerUserId, Draft("Blocked"), CancellationToken.None));
        Assert.Equal("This product is used by a published page.", ex.Message);

        var result = await service.UpdateAsync(creator.Slug, allowed.PublicId, creator.OwnerUserId, Draft("Allowed"), CancellationToken.None);
        Assert.Equal("Draft", result.Status);
    }

    // --- seeding -------------------------------------------------------------------------------------------

    private sealed record SeededProduct(int Id, Guid PublicId);

    private sealed record SeededPage(int Id, Guid PublicId);

    // ---- Six-month sales trend on the list ----------------------------------------------------------------

    [Fact]
    public async Task List_CarriesASixMonthTrendOfPaidOrders_OldestFirst()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        var sold = await CreateProductAsync(creator.CreatorId);
        var unsold = await CreateProductAsync(creator.CreatorId);
        var thisMonth = MonthStart().AddDays(1);

        await InsertOrderAsync(creator.CreatorId, sold.Id, 1000, "Paid", thisMonth, thisMonth);
        await InsertOrderAsync(creator.CreatorId, sold.Id, 1000, "Paid", null, thisMonth); // before PaidAt existed
        await InsertOrderAsync(creator.CreatorId, sold.Id, 1000, "Paid", MonthStart().AddMonths(-2).AddDays(3), MonthStart().AddMonths(-2));
        await InsertOrderAsync(creator.CreatorId, sold.Id, 1000, "Paid", MonthStart().AddMonths(-5), MonthStart().AddMonths(-5));
        await InsertOrderAsync(creator.CreatorId, sold.Id, 1000, "Paid", MonthStart().AddMonths(-6).AddDays(10), MonthStart().AddMonths(-6)); // too old
        await InsertOrderAsync(creator.CreatorId, sold.Id, 1000, "Refunded", thisMonth, thisMonth);
        await InsertOrderAsync(creator.CreatorId, sold.Id, 1000, "Pending", null, thisMonth);
        await InsertOrderAsync(other.CreatorId, sold.Id, 1000, "Paid", thisMonth, thisMonth); // another workspace

        await using var db = _fixture.CreateDbContext();
        var products = (await CreateService(db).ListAsync(creator.Slug, creator.OwnerUserId, false, CancellationToken.None))
            .ToDictionary(p => p.PublicId);

        Assert.Equal([1, 0, 0, 1, 0, 2], products[sold.PublicId].SalesTrend);
        Assert.Equal([0, 0, 0, 0, 0, 0], products[unsold.PublicId].SalesTrend);
    }

    private async Task<SeededProduct> CreateProductAsync(int creatorId, string status = "Active")
    {
        await using var db = _fixture.CreateDbContext();
        var publicId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName", "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, {$"Product {publicId:N}"[..20]}, 1000, {status}, 'productfile/test/guide.pdf', 'guide.pdf', 1024, 'application/pdf', now(), {now}, {now})
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
                                       "StripeCheckoutSessionId", "CreatedAt", "PaidAt", "UpdatedAt", "RefundedCents")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {TestData.UniqueEmail("buyer")}, {amountCents},
                    'Eur', {status}, 0, 0, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {createdAt}, {paidAt}, {createdAt},
                    {(status == "Refunded" ? amountCents : 0)})
            """);
    }
}
