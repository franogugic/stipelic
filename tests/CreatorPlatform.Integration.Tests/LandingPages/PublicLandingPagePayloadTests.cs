using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Services;
using CreatorPlatform.LandingPages.Infrastructure.Repositories;
using CreatorPlatform.LandingPages.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.LandingPages;

/// <summary>What a visitor gets: the page with the creator's brand and the product's type and currency, and the
/// creator's other live pages for "More from {brand}". Drafts are never visible.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PublicLandingPagePayloadTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public PublicLandingPagePayloadTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<T> WithServiceAsync<T>(Func<PublicLandingPageService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(new PublicLandingPageService(
            new LandingPageRepository(db), new LandingPageSectionRepository(db), new CreatorContextProvider(db)));
    }

    [Fact]
    public async Task ThePublicPage_CarriesTheBrand_AndTheProductTypeAndCurrency()
    {
        var creator = await _data.CreateCreatorAsync();
        await AddSettingsAsync(creator.CreatorId, "Acme Studio", "#E2553A", "https://cdn.example.test/logo.png");
        var productId = await AddProductAsync(creator.CreatorId, "Course", 2900, "https://cdn.example.test/cover.png");
        var slug = await AddPageAsync(creator.CreatorId, productId, "Sales", "Published");

        var page = await WithServiceAsync(s => s.GetPublishedAsync(creator.Slug, slug, CancellationToken.None));

        Assert.NotNull(page);
        Assert.Equal(new PublicCreatorBrandDto("Acme Studio", "#E2553A", "https://cdn.example.test/logo.png"), page.Creator);
        Assert.Equal(new PublicProductDto("Course", "Eur"), page.Product);
    }

    [Fact]
    public async Task WithoutBrandSettings_TheWorkspaceNameIsTheBrand()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId, "Digital", 0, null);
        var slug = await AddPageAsync(creator.CreatorId, productId, "LeadGen", "Published");

        var page = await WithServiceAsync(s => s.GetPublishedAsync(creator.Slug, slug, CancellationToken.None));

        Assert.Equal(new PublicCreatorBrandDto("Test Creator", null, null), page!.Creator);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Archived")]
    public async Task AnUnpublishedPage_IsTheSameAsAMissingOne(string status)
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId, "Digital", 1000, null);
        var slug = await AddPageAsync(creator.CreatorId, productId, "Sales", status);

        Assert.Null(await WithServiceAsync(s => s.GetPublishedAsync(creator.Slug, slug, CancellationToken.None)));
        Assert.Null(await WithServiceAsync(s => s.GetPublishedAsync(creator.Slug, "never-existed", CancellationToken.None)));
    }

    [Fact]
    public async Task CreatorPages_ListOnlyPublishedPages_NewestFirst_AtMostSix()
    {
        var creator = await _data.CreateCreatorAsync();
        await AddSettingsAsync(creator.CreatorId, "Acme Studio", "#E2553A", null);
        var productId = await AddProductAsync(creator.CreatorId, "Digital", 1500, "https://cdn.example.test/thumb.png");
        await AddPageAsync(creator.CreatorId, productId, "Sales", "Draft");
        await AddPageAsync(creator.CreatorId, productId, "Sales", "Archived");
        var published = new List<string>();
        for (var i = 0; i < 7; i++)
            published.Add(await AddPageAsync(creator.CreatorId, i == 6 ? null : productId, i % 2 == 0 ? "Sales" : "LeadGen", "Published", updatedMinutesAgo: 10 - i));

        var result = await WithServiceAsync(s => s.GetCreatorPagesAsync(creator.Slug, CancellationToken.None));

        Assert.NotNull(result);
        Assert.Equal(new PublicCreatorBrandDto("Acme Studio", "#E2553A", null), result.Creator);
        Assert.Equal(published.AsEnumerable().Reverse().Take(6), result.Pages.Select(p => p.Slug));
        Assert.Equal(new PublicCreatorPageDto("Page", published[6], "Sales", null, null, null), result.Pages[0]);
        Assert.Equal(new PublicCreatorPageDto("Page", published[5], "LeadGen", 1500, "Eur", "https://cdn.example.test/thumb.png"), result.Pages[1]);
    }

    [Fact]
    public async Task CreatorPages_ForAnUnknownOrDeletedWorkspace_AreNull()
    {
        var creator = await _data.CreateCreatorAsync();
        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""UPDATE creators.creators SET "Status" = 'Disabled' WHERE "Id" = {creator.CreatorId}""");

        Assert.Null(await WithServiceAsync(s => s.GetCreatorPagesAsync(creator.Slug, CancellationToken.None)));
        Assert.Null(await WithServiceAsync(s => s.GetCreatorPagesAsync("no-such-creator", CancellationToken.None)));
    }

    private async Task AddSettingsAsync(int creatorId, string brandName, string color, string? logoUrl)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "LogoUrl", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, {brandName}, {logoUrl}, {color}, 'Europe/Zagreb', 'en', {now}, {now})
            """);
    }

    private async Task<int> AddProductAsync(int creatorId, string type, int priceCents, string? thumbnailUrl)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "ThumbnailUrl", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Product', {priceCents}, {type}, 'Active', {thumbnailUrl}, {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task<string> AddPageAsync(int creatorId, int? productId, string type, string status, int updatedMinutesAgo = 0)
    {
        await using var db = _fixture.CreateDbContext();
        var slug = $"p-{Guid.NewGuid():N}"[..20];
        var updatedAt = DateTimeOffset.UtcNow.AddMinutes(-updatedMinutesAgo);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, 'Page', {slug}, {type}, {status}, {updatedAt}, {updatedAt})
            """);
        return slug;
    }
}
