using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.LandingPages.Application.Dtos;
using CreatorPlatform.LandingPages.Application.Services;
using CreatorPlatform.LandingPages.Infrastructure.Persistence;
using CreatorPlatform.LandingPages.Infrastructure.Repositories;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Media.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Application.Media;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Media;

/// <summary>S3 addition: an image URL in our public container is accepted only as a Confirmed image upload of the same
/// creator — not a Pending one, not another creator's, not an unknown blob or one with a query — so the abandoned-uploads
/// job can never delete an image in use. External URLs pass. A page's sections are checked whatever key holds the URL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OwnedImageCheckTests
{
    // The development storage account's address for the "creator-media" container.
    private const string Container = "http://127.0.0.1:10000/devstoreaccount1/creator-media";
    private const string External = "https://images.example.test/cover.png";

    private static readonly IOptions<MediaOptions> Media = Options.Create(new MediaOptions
    {
        ConnectionString = "UseDevelopmentStorage=true",
        ContainerName = "creator-media",
        PrivateContainerName = "product-files",
    });

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public OwnedImageCheckTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<string> UploadAsync(int creatorId, bool confirmed, MediaUploadPurpose purpose = MediaUploadPurpose.LandingPageHero)
    {
        var path = $"{purpose}/{creatorId}/{Guid.NewGuid()}.png";
        var reference = BlobReference.CreatePending(creatorId, purpose, $"{Container}/{path}", path, "image/png", DateTimeOffset.UtcNow);
        if (confirmed)
            reference.Confirm(1024, DateTimeOffset.UtcNow);
        await using var db = _fixture.CreateDbContext();
        db.Add(reference);
        await db.SaveChangesAsync();
        return reference.BlobUrl;
    }

    private async Task CheckAsync(int? creatorId, params string?[] urls)
    {
        await using var db = _fixture.CreateDbContext();
        await new OwnedImageCheck(db, Media).EnsureOwnedAsync(creatorId, urls, CancellationToken.None);
    }

    private async Task RefusedAsync(int? creatorId, params string?[] urls)
    {
        var refused = await Assert.ThrowsAsync<BadRequestException>(() => CheckAsync(creatorId, urls));
        Assert.Equal(IOwnedImageCheck.ImageNotUploadedCode, refused.Code);
    }

    [Fact]
    public async Task TheCreatorsConfirmedUpload_AndExternalImages_Pass()
    {
        var creator = await _data.CreateCreatorAsync();
        var confirmed = await UploadAsync(creator.CreatorId, confirmed: true);

        await CheckAsync(creator.CreatorId, confirmed, External, null, "", "  ");
        await CheckAsync(null, External);
    }

    [Fact]
    public async Task APendingUpload_AnotherCreatorsUpload_AnUnknownBlob_AndAQuery_AreRefused()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        var pending = await UploadAsync(creator.CreatorId, confirmed: false);
        var someoneElses = await UploadAsync(other.CreatorId, confirmed: true);
        var confirmed = await UploadAsync(creator.CreatorId, confirmed: true);

        await RefusedAsync(creator.CreatorId, pending);
        await RefusedAsync(creator.CreatorId, someoneElses);
        await RefusedAsync(creator.CreatorId, $"{Container}/LandingPageHero/{creator.CreatorId}/{Guid.NewGuid()}.png");
        await RefusedAsync(creator.CreatorId, $"{confirmed}?sv=2024&sig=x");
        await RefusedAsync(creator.CreatorId, confirmed, pending);
        await RefusedAsync(null, confirmed);
    }

    [Fact]
    public void EveryStringInASectionsContent_IsFound_WhateverTheKeyOrNesting()
    {
        var strings = JsonStrings.All("""
            {"heading":"Hi","imageUrl":"a","gallery":{"imageUrls":["b","c"]},"items":[{"logoUrl":"d","n":3,"ok":true,"x":null}]}
            """);

        Assert.Equal(["Hi", "a", "b", "c", "d"], strings);
    }

    [Fact]
    public async Task SavingAPage_RefusesASectionImageThatIsNotAConfirmedUpload_AndAcceptsOne()
    {
        var creator = await _data.CreateCreatorAsync();
        var pending = await UploadAsync(creator.CreatorId, confirmed: false);
        var confirmed = await UploadAsync(creator.CreatorId, confirmed: true);

        Guid pagePublicId;
        LandingPageWithSectionsResponseDto page;
        var productPublicId = Guid.NewGuid();
        await using (var db = _fixture.CreateDbContext())
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName",
                                               "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
                VALUES ({productPublicId}, {creator.CreatorId}, 'Guide', 0, 'Active', 'productfile/test/guide.pdf', 'guide.pdf', 1024,
                        'application/pdf', now(), now(), now())
                """);
            var service = Pages(db);
            var created = await service.CreateAsync(creator.Slug, creator.OwnerUserId, new CreateLandingPageRequestDto
            {
                Title = "Guide", Slug = $"guide-{Guid.NewGuid():N}"[..20], Type = "LeadGen", ProductId = productPublicId
            }, default);
            pagePublicId = created.PublicId;
            page = await service.GetWithSectionsAsync(creator.Slug, pagePublicId, creator.OwnerUserId, default);
        }

        SaveLandingPageRequestDto WithHeroImages(params string[] urls) => new()
        {
            Title = page.Title,
            Slug = page.Slug,
            Type = page.Type,
            Sections = page.Sections.Select(s => new SaveLandingPageSectionDto
            {
                PublicId = s.PublicId,
                Type = s.Type,
                Variant = s.Variant,
                SortOrder = s.SortOrder,
                BackgroundColor = s.BackgroundColor,
                ContentJson = s.Type == "Hero"
                    ? System.Text.Json.JsonSerializer.Serialize(new { heading = "Hi", imageUrl = urls[0], extra = new { imageUrls = urls.Skip(1) } })
                    : s.ContentJson
            }).ToList()
        };

        await using (var db = _fixture.CreateDbContext())
        {
            var refused = await Assert.ThrowsAsync<BadRequestException>(() =>
                Pages(db).SaveEditorAsync(creator.Slug, pagePublicId, creator.OwnerUserId, WithHeroImages(confirmed, pending), default));
            Assert.Equal(IOwnedImageCheck.ImageNotUploadedCode, refused.Code);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var saved = await Pages(db).SaveEditorAsync(creator.Slug, pagePublicId, creator.OwnerUserId, WithHeroImages(confirmed, External), default);
            Assert.Contains(confirmed, saved.Sections.Single(s => s.Type == "Hero").ContentJson);
        }
    }

    private static LandingPageService Pages(CreatorPlatformDbContext db) => new(
        new LandingPageRepository(db),
        new LandingPageSectionRepository(db),
        new CreatorPlatform.LandingPages.Infrastructure.Services.CreatorContextProvider(db),
        new LandingPagesUnitOfWork(db),
        new OwnedImageCheck(db, Media));
}
