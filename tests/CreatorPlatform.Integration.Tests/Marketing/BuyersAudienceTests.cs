using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CreatorPlatform.Integration.Tests.Marketing;

/// <summary>The "buyers of a landing page" audience: only Paid orders through that page, one email per person (case and
/// spaces ignored), minus unsubscribes, scoped to the creator; the overview lists the creator's Sales pages.</summary>
[Collection(PostgresCollection.Name)]
public sealed class BuyersAudienceTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public BuyersAudienceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private sealed record Seeded(TestData.SeededCreator Creator, int ProductId, int PageId, Guid PagePublicId);

    private async Task<Seeded> SeedAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId);
        var (pageId, publicId) = await AddPageAsync(creator.CreatorId, productId, "Sales", "Published", "Main sales page");
        return new Seeded(creator, productId, pageId, publicId);
    }

    private async Task<T> WithAudienceAsync<T>(Func<AudienceService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(new AudienceService(db));
    }

    [Fact]
    public async Task OnlyPaidOrdersOfThePageCount_OnePersonOnce_UnsubscribedLeftOut()
    {
        var s = await SeedAsync();
        var otherPage = await AddPageAsync(s.Creator.CreatorId, s.ProductId, "Sales", "Published", "Another page");
        var foreign = await _data.CreateCreatorAsync();
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "  Ana@Example.TEST ");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "ana@example.test");   // the same person again
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "iva@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Refunded", "refunded@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Pending", "pending@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Failed", "failed@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", null);                  // no address to write to
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "gone@example.test");
        await AddUnsubscribeAsync(s.Creator.CreatorId, "gone@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, otherPage.PageId, "Paid", "elsewhere@example.test"); // another page
        await AddOrderAsync(foreign.CreatorId, s.ProductId, s.PageId, "Paid", "foreign@example.test");             // another creator's order
        await AddUnsubscribeAsync(foreign.CreatorId, "iva@example.test");                                          // another creator's opt-out

        var count = await WithAudienceAsync(a => a.GetAudienceCountAsync(
            CampaignAudienceType.LandingPageBuyers, s.PageId, null, null, s.Creator.CreatorId, CancellationToken.None));
        var emails = await WithAudienceAsync(a => a.GetAudienceEmailsAsync(
            CampaignAudienceType.LandingPageBuyers, s.PageId, null, null, s.Creator.CreatorId, CancellationToken.None));

        Assert.Equal(["ana@example.test", "iva@example.test"], emails);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task APersonWhoUnsubscribedLater_IsNoLongerInTheAudience()
    {
        var s = await SeedAsync();
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "ana@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "iva@example.test");

        Task<List<string>> Emails() => WithAudienceAsync(a => a.GetAudienceEmailsAsync(
            CampaignAudienceType.LandingPageBuyers, s.PageId, null, null, s.Creator.CreatorId, CancellationToken.None));

        Assert.Equal(2, (await Emails()).Count);
        await AddUnsubscribeAsync(s.Creator.CreatorId, "ana@example.test");
        Assert.Equal(["iva@example.test"], await Emails());
    }

    [Fact]
    public async Task TheKeysetPage_WalksTheBuyersInOrder_WithoutRepeats()
    {
        var s = await SeedAsync();
        foreach (var name in new[] { "d", "b", "a", "c", "e" })
            await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", $"{name}@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "B@example.test"); // a repeat of b

        var first = await WithAudienceAsync(a => a.GetAudiencePageAsync(
            CampaignAudienceType.LandingPageBuyers, s.PageId, null, null, s.Creator.CreatorId, null, 2, CancellationToken.None));
        var second = await WithAudienceAsync(a => a.GetAudiencePageAsync(
            CampaignAudienceType.LandingPageBuyers, s.PageId, null, null, s.Creator.CreatorId, first.Emails[^1], 2, CancellationToken.None));
        var third = await WithAudienceAsync(a => a.GetAudiencePageAsync(
            CampaignAudienceType.LandingPageBuyers, s.PageId, null, null, s.Creator.CreatorId, second.Emails[^1], 2, CancellationToken.None));

        Assert.Equal(["a@example.test", "b@example.test"], first.Emails);
        Assert.True(first.HasMore);
        Assert.Equal(["c@example.test", "d@example.test"], second.Emails);
        Assert.True(second.HasMore);
        Assert.Equal(["e@example.test"], third.Emails);
        Assert.False(third.HasMore);
    }

    [Fact]
    public async Task TheOverview_ListsTheSalesPages_ArchivedOnesOnlyWithBuyers_BiggestFirst()
    {
        var s = await SeedAsync();   // "Main sales page": 2 buyers
        var draftEmpty = await AddPageAsync(s.Creator.CreatorId, s.ProductId, "Sales", "Draft", "Draft, no buyers");
        var archivedEmpty = await AddPageAsync(s.Creator.CreatorId, s.ProductId, "Sales", "Archived", "Archived, no buyers");
        var archivedBuyers = await AddPageAsync(s.Creator.CreatorId, s.ProductId, "Sales", "Archived", "Archived with a buyer");
        var leadGen = await AddPageAsync(s.Creator.CreatorId, s.ProductId, "LeadGen", "Published", "A sign-up page");
        var busiest = await AddPageAsync(s.Creator.CreatorId, s.ProductId, "Sales", "Published", "Busiest page");
        var foreign = await _data.CreateCreatorAsync();
        var foreignProduct = await AddProductAsync(foreign.CreatorId);
        var foreignPage = await AddPageAsync(foreign.CreatorId, foreignProduct, "Sales", "Published", "Someone else’s page");

        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "ana@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "iva@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, s.PageId, "Paid", "gone@example.test");   // unsubscribed below
        await AddUnsubscribeAsync(s.Creator.CreatorId, "gone@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, archivedBuyers.PageId, "Paid", "old@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, archivedEmpty.PageId, "Refunded", "refunded@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, draftEmpty.PageId, "Pending", "pending@example.test");
        await AddOrderAsync(s.Creator.CreatorId, s.ProductId, leadGen.PageId, "Paid", "lead@example.test");
        foreach (var name in new[] { "p", "q", "r" })
            await AddOrderAsync(s.Creator.CreatorId, s.ProductId, busiest.PageId, "Paid", $"{name}@example.test");
        await AddOrderAsync(foreign.CreatorId, foreignProduct, foreignPage.PageId, "Paid", "foreign@example.test");

        var overview = await WithAudienceAsync(a => a.GetAudienceOverviewAsync(s.Creator.CreatorId, CancellationToken.None));

        Assert.Equal(
            [("Busiest page", 3), ("Main sales page", 2), ("Archived with a buyer", 1), ("Draft, no buyers", 0)],
            overview.LandingPageBuyers.Select(page => (page.Title, page.RecipientCount)).ToList());
        Assert.DoesNotContain(overview.LandingPageBuyers, page => page.PublicId == archivedEmpty.PublicId);
        Assert.DoesNotContain(overview.LandingPageBuyers, page => page.PublicId == leadGen.PublicId);
        Assert.DoesNotContain(overview.LandingPageBuyers, page => page.PublicId == foreignPage.PublicId);
        Assert.Equal(s.PagePublicId, overview.LandingPageBuyers[1].PublicId);
        // The other audiences are untouched.
        Assert.Equal(0, overview.All.RecipientCount);
    }

    [Fact]
    public async Task ACreatorWithoutSalesPages_HasNoBuyerAudiences()
    {
        var creator = await _data.CreateCreatorAsync();

        var overview = await WithAudienceAsync(a => a.GetAudienceOverviewAsync(creator.CreatorId, CancellationToken.None));

        Assert.Empty(overview.LandingPageBuyers);
    }

    [Fact]
    public async Task TheDatabaseAcceptsABuyersCampaignOnlyWithALandingPageAndNoProduct()
    {
        var s = await SeedAsync();

        await InsertCampaignAsync(s.Creator.CreatorId, "LandingPageBuyers", s.PageId, null);   // accepted
        await InsertCampaignAsync(s.Creator.CreatorId, "LandingPage", s.PageId, null);         // still accepted
        await InsertCampaignAsync(s.Creator.CreatorId, "Product", null, s.ProductId);          // still accepted
        await InsertCampaignAsync(s.Creator.CreatorId, "All", null, null);                     // still accepted

        await Assert.ThrowsAsync<PostgresException>(() => InsertCampaignAsync(s.Creator.CreatorId, "LandingPageBuyers", null, null));
        await Assert.ThrowsAsync<PostgresException>(() => InsertCampaignAsync(s.Creator.CreatorId, "LandingPageBuyers", s.PageId, s.ProductId));
        await Assert.ThrowsAsync<PostgresException>(() => InsertCampaignAsync(s.Creator.CreatorId, "LandingPageBuyers", null, s.ProductId));
        await Assert.ThrowsAsync<PostgresException>(() => InsertCampaignAsync(s.Creator.CreatorId, "Product", s.PageId, s.ProductId));
    }

    // ---- seeding ------------------------------------------------------------------------------------------

    private async Task<int> AddProductAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName", "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Product', 1000, 'Active', 'productfile/test/guide.pdf', 'guide.pdf', 1024, 'application/pdf', now(), {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task<(int PageId, Guid PublicId)> AddPageAsync(int creatorId, int productId, string type, string status, string title)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var publicId = Guid.NewGuid();
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, {productId}, {title}, {$"p-{Guid.NewGuid():N}"[..20]}, {type}, {status}, {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return (id, publicId);
    }

    private async Task AddOrderAsync(int creatorId, int productId, int landingPageId, string status, string? email)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "LandingPageId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "PaidAt", "UpdatedAt", "RefundedCents")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {landingPageId}, {email}, 1000, 'Eur', {status},
                    1000, 100, 'StripeConnect', {$"cs_test_{Guid.NewGuid():N}"}, {now}, {now}, {now}, {(status == "Refunded" ? 1000 : 0)})
            """);
    }

    private async Task AddUnsubscribeAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.unsubscribes ("CreatorId", "Email", "Source", "UnsubscribedAt")
            VALUES ({creatorId}, {email}, 'Link', {DateTimeOffset.UtcNow})
            """);
    }

    private async Task InsertCampaignAsync(int creatorId, string audienceType, int? landingPageId, int? productId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.campaigns ("PublicId", "CreatorId", "Subject", "BodyText", "AudienceType", "LandingPageId", "ProductId",
                                             "Status", "RecipientCount", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Hi', 'Body', {audienceType}, {landingPageId}, {productId}, 'Draft', 0, {now}, {now})
            """);
    }
}
