using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>What the buyer's order email loads about the product and the creator's brand.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderEmailContextTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public OrderEmailContextTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    [Fact]
    public async Task WithBrandSettings_TheEmailUsesTheBrand()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId, "Course", "https://cdn.example.test/cover.png");
        await using (var db = _fixture.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO creators.creator_settings ("CreatorId", "SupportEmail", "BrandName", "LogoUrl", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
                VALUES ({creator.CreatorId}, 'help@acme.test', 'Acme', 'https://cdn.example.test/logo.png', '#E2553A', 'Europe/Zagreb', 'en', {now}, {now})
                """);
        }

        await using var read = _fixture.CreateDbContext();
        var context = await new CreatorContextProvider(read).GetOrderEmailContextAsync(productId, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(
            ("Kept product", "Online course", "https://cdn.example.test/cover.png", "Acme", "#E2553A", "https://cdn.example.test/logo.png", "help@acme.test"),
            (context.ProductName, context.ProductTypeLabel, context.ProductThumbnailUrl, context.CreatorName, context.BrandColor, context.LogoUrl, context.SupportEmail));
    }

    [Fact]
    public async Task WithoutBrandSettings_TheWorkspaceNameAndNoBrand()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId, "Digital", null);

        await using var db = _fixture.CreateDbContext();
        var context = await new CreatorContextProvider(db).GetOrderEmailContextAsync(productId, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(("Digital download", "Test Creator"), (context.ProductTypeLabel, context.CreatorName));
        Assert.Null(context.BrandColor);
        Assert.Null(context.SupportEmail);
        Assert.Null(await new CreatorContextProvider(db).GetOrderEmailContextAsync(int.MaxValue, CancellationToken.None));
    }

    private async Task<int> AddProductAsync(int creatorId, string type, string? thumbnailUrl)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "ThumbnailUrl", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Kept product', 2900, {type}, 'Active', {thumbnailUrl}, {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }
}
