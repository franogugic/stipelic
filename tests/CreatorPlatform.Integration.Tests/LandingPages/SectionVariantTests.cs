using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Services;
using CreatorPlatform.LandingPages.Application.Templates;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.LandingPages.Infrastructure.Persistence;
using CreatorPlatform.LandingPages.Infrastructure.Repositories;
using CreatorPlatform.LandingPages.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.LandingPages;

/// <summary>Each section stores its layout (variant) and may have no background colour (the page default).</summary>
[Collection(PostgresCollection.Name)]
public sealed class SectionVariantTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public SectionVariantTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private static LandingPageService CreateService(CreatorPlatformDbContext db) => new(
        new LandingPageRepository(db),
        new LandingPageSectionRepository(db),
        new CreatorContextProvider(db),
        new LandingPagesUnitOfWork(db));

    private async Task<T> WithServiceAsync<T>(Func<LandingPageService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(CreateService(db));
    }

    /// <summary>A new page through the service, so it starts with the default starter sections.</summary>
    private async Task<(TestData.SeededCreator Creator, LandingPageWithSectionsResponseDto Page)> CreatePageAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        Guid productPublicId;
        await using (var db = _fixture.CreateDbContext())
        {
            productPublicId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "CreatedAt", "UpdatedAt")
                VALUES ({productPublicId}, {creator.CreatorId}, 'Guide', 0, 'Digital', 'Active', {now}, {now})
                """);
        }

        var created = await WithServiceAsync(s => s.CreateAsync(creator.Slug, creator.OwnerUserId, new CreateLandingPageRequestDto
        {
            Title = "Guide",
            Slug = $"guide-{Guid.NewGuid():N}"[..20],
            Type = "LeadGen",
            ProductId = productPublicId
        }, CancellationToken.None));
        var page = await WithServiceAsync(s => s.GetWithSectionsAsync(creator.Slug, created.PublicId, creator.OwnerUserId, CancellationToken.None));
        return (creator, page);
    }

    private static SaveLandingPageRequestDto SaveRequest(
        LandingPageWithSectionsResponseDto page, Func<LandingPageSectionResponseDto, SaveLandingPageSectionDto>? map = null) => new()
    {
        Title = page.Title,
        Slug = page.Slug,
        Type = page.Type,
        Sections = page.Sections.Select(map ?? (s => new SaveLandingPageSectionDto
        {
            PublicId = s.PublicId,
            Type = s.Type,
            Variant = s.Variant,
            SortOrder = s.SortOrder,
            BackgroundColor = s.BackgroundColor,
            ContentJson = s.ContentJson
        })).ToList()
    };

    private Task<LandingPageWithSectionsResponseDto> SaveAsync(TestData.SeededCreator creator, LandingPageWithSectionsResponseDto page, SaveLandingPageRequestDto request) =>
        WithServiceAsync(s => s.SaveEditorAsync(creator.Slug, page.PublicId, creator.OwnerUserId, request, CancellationToken.None));

    [Fact]
    public async Task ANewPage_StartsWithEachTypesDefaultVariant_AndThePageDefaultBackground()
    {
        var (_, page) = await CreatePageAsync();

        Assert.Equal(
            [("Navbar", "simple"), ("Hero", "split"), ("Cta", "banner"), ("Footer", "simple")],
            page.Sections.Select(s => (s.Type, s.Variant)));
        Assert.All(page.Sections, s => Assert.Null(s.BackgroundColor));
    }

    [Fact]
    public async Task Save_ChangesTheVariant_AndANullBackgroundRoundTrips()
    {
        var (creator, page) = await CreatePageAsync();
        var request = SaveRequest(page, s => new SaveLandingPageSectionDto
        {
            PublicId = s.PublicId,
            Type = s.Type,
            Variant = s.Type == "Hero" ? "Centered" : s.Variant,
            SortOrder = s.SortOrder,
            BackgroundColor = s.Type == "Cta" ? "#F3ECE2" : null,
            ContentJson = s.ContentJson
        });

        await SaveAsync(creator, page, request);
        var reloaded = await WithServiceAsync(s => s.GetWithSectionsAsync(creator.Slug, page.PublicId, creator.OwnerUserId, CancellationToken.None));

        var hero = reloaded.Sections.Single(s => s.Type == "Hero");
        Assert.Equal(("centered", (string?)null), (hero.Variant, hero.BackgroundColor));
        Assert.Equal("#f3ece2", reloaded.Sections.Single(s => s.Type == "Cta").BackgroundColor);
    }

    [Fact]
    public async Task Save_WithoutAVariant_KeepsTheExistingOne_AndGivesANewSectionTheDefault()
    {
        var (creator, page) = await CreatePageAsync();
        var heroId = page.Sections.Single(s => s.Type == "Hero").PublicId;
        await SaveAsync(creator, page, SaveRequest(page, s => WithVariant(s, s.PublicId == heroId ? "centered" : s.Variant)));

        // An older client that sends no variant: the hero keeps "centered", the new FAQ gets the default.
        var request = SaveRequest(page, s => WithVariant(s, null));
        request.Sections.Insert(2, new SaveLandingPageSectionDto { Type = "Faq", Variant = "", ContentJson = "{}" });
        var saved = await SaveAsync(creator, page, request);

        Assert.Equal("centered", saved.Sections.Single(s => s.Type == "Hero").Variant);
        Assert.Equal("accordion", saved.Sections.Single(s => s.Type == "Faq").Variant);
    }

    private static SaveLandingPageSectionDto WithVariant(LandingPageSectionResponseDto s, string? variant) => new()
    {
        PublicId = s.PublicId,
        Type = s.Type,
        Variant = variant,
        SortOrder = s.SortOrder,
        BackgroundColor = s.BackgroundColor,
        ContentJson = s.ContentJson
    };

    [Theory]
    [InlineData("Hero", "mosaic")]
    [InlineData("Navbar", "does-not-exist")]
    public async Task Save_AVariantThatIsNotTheTypes_Is400(string type, string variant)
    {
        var (creator, page) = await CreatePageAsync();
        var request = SaveRequest(page, s => new SaveLandingPageSectionDto
        {
            PublicId = s.PublicId,
            Type = s.Type,
            Variant = s.Type == type ? variant : s.Variant,
            ContentJson = s.ContentJson
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => SaveAsync(creator, page, request));
        Assert.StartsWith($"Invalid layout for a {type} section.", exception.Message);
    }

    [Fact]
    public async Task Save_ContentOver64KB_Is400()
    {
        var (creator, page) = await CreatePageAsync();
        var huge = $$"""{"heading":"{{new string('x', 64 * 1024)}}"}""";
        var request = SaveRequest(page, s => new SaveLandingPageSectionDto
        {
            PublicId = s.PublicId,
            Type = s.Type,
            Variant = s.Variant,
            ContentJson = s.Type == "Hero" ? huge : s.ContentJson
        });

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => SaveAsync(creator, page, request));
        Assert.Equal("Section content is too large (max 64 KB per section).", exception.Message);
    }

    [Fact]
    public async Task TheMigrationBackfill_GivesEveryTypeItsDefaultVariant()
    {
        var landingPageId = await _data.CreateLandingPageAsync((await _data.CreateCreatorAsync()).CreatorId);
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var types = Enum.GetValues<LandingPageSectionType>();
        for (var i = 0; i < types.Length; i++)
        {
            // A row as it was before the column existed (the migration adds it with '' first).
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO landing_pages.landing_page_sections ("PublicId", "LandingPageId", "Type", "Variant", "SortOrder", "BackgroundColor", "ContentJson", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {landingPageId}, {types[i].ToString()}, '', {i}, '#ffffff', {"{}"}::jsonb, {now}, {now})
                """);
        }

        await db.Database.ExecuteSqlRawAsync(AddSectionVariant.BackfillVariantsSql);

        var rows = await db.Database.SqlQuery<BackfilledRow>($"""
            SELECT "Type", "Variant", "BackgroundColor" FROM landing_pages.landing_page_sections WHERE "LandingPageId" = {landingPageId}
            """).ToListAsync();
        Assert.Equal(types.Length, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(SectionTemplates.GetDefault(Enum.Parse<LandingPageSectionType>(row.Type)).Variant, row.Variant);
            Assert.Equal("#ffffff", row.BackgroundColor);
        });
    }

    private sealed record BackfilledRow(string Type, string Variant, string? BackgroundColor);
}
