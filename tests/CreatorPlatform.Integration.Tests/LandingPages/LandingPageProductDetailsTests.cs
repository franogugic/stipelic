using System.Text.Json;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.LandingPages.Application.Services;
using CreatorPlatform.LandingPages.Infrastructure.Persistence;
using CreatorPlatform.LandingPages.Infrastructure.Repositories;
using CreatorPlatform.LandingPages.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.LandingPages;

/// <summary>The owner-facing landing page DTOs carry the product's public id, name and thumbnail — never the
/// internal product id.</summary>
[Collection(PostgresCollection.Name)]
public sealed class LandingPageProductDetailsTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public LandingPageProductDetailsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private LandingPageService CreateService(Shared.Infrastructure.Persistence.CreatorPlatformDbContext db) => new(
        new LandingPageRepository(db),
        new LandingPageSectionRepository(db),
        new CreatorContextProvider(db),
        new LandingPagesUnitOfWork(db));

    private sealed record SeededProduct(int Id, Guid PublicId, string Name, string? ThumbnailUrl);

    private async Task<SeededProduct> CreateProductAsync(int creatorId, string? thumbnailUrl)
    {
        await using var db = _fixture.CreateDbContext();
        var publicId = Guid.NewGuid();
        var name = $"Product {publicId:N}"[..20];
        var now = DateTimeOffset.UtcNow;
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "ThumbnailUrl",
                                           "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, {name}, 1000, 'Digital', 'Active', {thumbnailUrl}, {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return new SeededProduct(id, publicId, name, thumbnailUrl);
    }

    private async Task<Guid> AttachProductAsync(int landingPageId, int? productId)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            UPDATE landing_pages.landing_pages SET "ProductId" = {productId} WHERE "Id" = {landingPageId}
            """);
        return (await db.Database.SqlQuery<Guid>($"""
            SELECT "PublicId" AS "Value" FROM landing_pages.landing_pages WHERE "Id" = {landingPageId}
            """).ToListAsync()).Single();
    }

    [Fact]
    public async Task List_CarriesEachPagesProductDetails()
    {
        var creator = await _data.CreateCreatorAsync();
        var withThumbnail = await CreateProductAsync(creator.CreatorId, "https://cdn.example.test/cover.png");
        var withoutThumbnail = await CreateProductAsync(creator.CreatorId, null);
        var pageA = await AttachProductAsync(await _data.CreateLandingPageAsync(creator.CreatorId), withThumbnail.Id);
        var pageB = await AttachProductAsync(await _data.CreateLandingPageAsync(creator.CreatorId), withoutThumbnail.Id);
        var pageC = await AttachProductAsync(await _data.CreateLandingPageAsync(creator.CreatorId), withThumbnail.Id);
        var noProduct = await AttachProductAsync(await _data.CreateLandingPageAsync(creator.CreatorId), null);

        await using var db = _fixture.CreateDbContext();
        var pages = (await CreateService(db).ListAsync(creator.Slug, creator.OwnerUserId, false, CancellationToken.None))
            .ToDictionary(p => p.PublicId);

        Assert.Equal(withThumbnail.PublicId, pages[pageA].ProductPublicId);
        Assert.Equal(withThumbnail.Name, pages[pageA].ProductName);
        Assert.Equal(withThumbnail.ThumbnailUrl, pages[pageA].ProductThumbnailUrl);
        Assert.Equal(withThumbnail.PublicId, pages[pageC].ProductPublicId);
        Assert.Equal(withoutThumbnail.PublicId, pages[pageB].ProductPublicId);
        Assert.Equal(withoutThumbnail.Name, pages[pageB].ProductName);
        Assert.Null(pages[pageB].ProductThumbnailUrl);
        Assert.Null(pages[noProduct].ProductPublicId);
        Assert.Null(pages[noProduct].ProductName);
    }

    [Fact]
    public async Task EditorPage_CarriesTheProductDetails_AndNoInternalIdIsSerialized()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId, "https://cdn.example.test/cover.png");
        var pageId = await AttachProductAsync(await _data.CreateLandingPageAsync(creator.CreatorId), product.Id);

        await using var db = _fixture.CreateDbContext();
        var service = CreateService(db);
        var page = await service.GetWithSectionsAsync(creator.Slug, pageId, creator.OwnerUserId, CancellationToken.None);
        var listed = Assert.Single(await service.ListAsync(creator.Slug, creator.OwnerUserId, false, CancellationToken.None));

        Assert.Equal(product.PublicId, page.ProductPublicId);
        Assert.Equal(product.Name, page.ProductName);
        Assert.Equal(1000, page.ProductPriceCents);
        Assert.Equal(product.ThumbnailUrl, page.ProductThumbnailUrl);

        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var json in new[] { JsonSerializer.Serialize(page, web), JsonSerializer.Serialize(listed, web) })
        {
            using var document = JsonDocument.Parse(json);
            var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
            Assert.DoesNotContain("productId", names);
            Assert.DoesNotContain("id", names);
            Assert.Contains("productPublicId", names);
        }
    }
}
