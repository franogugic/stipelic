using CreatorPlatform.Access.Application.Services;
using CreatorPlatform.Access.Infrastructure.Services;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Domain.Media;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Options;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Persistence;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Payouts.Infrastructure.Repositories;
using CreatorPlatform.Products.Application.Dtos;
using CreatorPlatform.Products.Application.Services;
using CreatorPlatform.Products.Infrastructure.Persistence;
using CreatorPlatform.Products.Infrastructure.Repositories;
using CreatorPlatform.Products.Infrastructure.Services;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using OrdersCreatorContextProvider = CreatorPlatform.Orders.Infrastructure.Services.CreatorContextProvider;
using ProductsCreatorContextProvider = CreatorPlatform.Products.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Products;

/// <summary>
/// F8.1 against the real database: a paid order gets exactly one personal link and a refund turns it off; downloads
/// count in one statement and always hand out the product's current file; a lead page's new sign-up gets one email and a
/// repeat at most one more per 24 hours; a sign-up rejected by the contact limit sends nothing; the migration step.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ProductAccessTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private const string LeadMagnetPurpose = "LeadMagnetDelivery";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly InMemoryProductFileStorage _storage = new();

    public ProductAccessTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private sealed record SeededProduct(int Id, Guid PublicId, string BlobPath);

    private async Task<SeededProduct> CreateProductAsync(int creatorId, int priceCents = 2900, string fileName = "Presets.zip")
    {
        await using var db = _fixture.CreateDbContext();
        var publicId = Guid.NewGuid();
        var blobPath = $"ProductFile/{creatorId}/{Guid.NewGuid()}.zip";
        _storage.Put(blobPath, "application/zip", [0x50, 0x4B, 0x03, 0x04]);
        var id = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "FileBlobPath", "FileName",
                                           "FileSizeBytes", "FileContentType", "FileUploadedAt", "CreatedAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, 'Adriatic Presets', {priceCents}, 'Active', {blobPath}, {fileName},
                    18350080, 'application/zip', now(), now(), now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        return new SeededProduct(id, publicId, blobPath);
    }

    /// <summary>A Pending order as checkout leaves it; returns its Checkout session id.</summary>
    private async Task<string> CreatePendingOrderAsync(int creatorId, int productId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        var sessionId = $"cs_test_{Guid.NewGuid():N}";
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {email}, 2900, 'Eur', 'Pending', 0, 0, 'StripeConnect',
                    {sessionId}, now(), now())
            """);
        return sessionId;
    }

    private async Task<int> CreatePageAsync(int creatorId, int? productId, string type = "LeadGen", string status = "Published")
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status",
                                                     "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, 'Guide', {$"p-{Guid.NewGuid():N}"}, {type}, {status}, now(), now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private static OrderWebhookService Webhooks(CreatorPlatformDbContext db) => new(
        new OrderRepository(db),
        new OrdersUnitOfWork(db),
        new OrdersCreatorContextProvider(db),
        new EmailOutboxService(db, Options.Create(new EmailOptions { FrontendBaseUrl = "https://app.luma.test" })),
        new NoHomeSummaryCache(),
        new PayoutLedgerService(new LedgerEntryRepository(db), NullLogger<PayoutLedgerService>.Instance),
        new OrderAccessGrants(db),
        Options.Create(new OrdersOptions { FrontendBaseUrl = "https://app.luma.test" }),
        NullLogger<OrderWebhookService>.Instance);

    private ProductAccessService Access(CreatorPlatformDbContext db) =>
        new(new AccessGrantReader(db), _storage, Options.Create(new MediaOptions { DownloadUrlExpiryMinutes = 10 }));

    private async Task PayAsync(string sessionId)
    {
        await using var db = _fixture.CreateDbContext();
        await Webhooks(db).HandleCheckoutSessionCompletedAsync(new OrderCheckoutCompletedDto(sessionId, $"pi_{sessionId}"), None);
    }

    private async Task RefundAsync(string sessionId)
    {
        await using var db = _fixture.CreateDbContext();
        await Webhooks(db).HandleChargeRefundedAsync(new OrderChargeRefundedDto($"pi_{sessionId}", "ch_1"), None);
    }

    private sealed record GrantRow(string Token, string Email, DateTimeOffset? RevokedAt, int DownloadCount, DateTimeOffset? LastDownloadedAt);

    private async Task<List<GrantRow>> OrderGrantsAsync(string sessionId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<GrantRow>($"""
            SELECT g."Token", g."Email", g."RevokedAt", g."DownloadCount", g."LastDownloadedAt"
            FROM products.access_grants g JOIN orders.orders o ON o."Id" = g."OrderId"
            WHERE o."StripeCheckoutSessionId" = {sessionId}
            """).ToListAsync();
    }

    private async Task<List<GrantRow>> CaptureGrantsAsync(int landingPageId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<GrantRow>($"""
            SELECT g."Token", g."Email", g."RevokedAt", g."DownloadCount", g."LastDownloadedAt"
            FROM products.access_grants g JOIN analytics.email_captures c ON c."Id" = g."EmailCaptureId"
            WHERE c."LandingPageId" = {landingPageId} AND c."Email" = {email}
            """).ToListAsync();
    }

    private sealed record OutboxRow(string Subject, string HtmlBody, string PlainTextBody);

    private async Task<List<OutboxRow>> OutboxAsync(string toEmail, string purpose)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Database.SqlQuery<OutboxRow>($"""
            SELECT "Subject", "HtmlBody", "PlainTextBody" FROM email.email_outbox_messages
            WHERE "ToEmail" = {toEmail} AND "Purpose" = {purpose}
            ORDER BY "CreatedAt"
            """).ToListAsync();
    }

    // ---- Orders ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task APaidOrder_GetsExactlyOneLink_EvenWhenTheWebhookRepeats_AndItsEmailCarriesIt()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);
        var buyer = TestData.UniqueEmail("buyer");
        var sessionId = await CreatePendingOrderAsync(creator.CreatorId, product.Id, buyer);

        await PayAsync(sessionId);
        await PayAsync(sessionId);

        var grant = Assert.Single(await OrderGrantsAsync(sessionId));
        Assert.Equal(43, grant.Token.Length);
        Assert.Equal(buyer, grant.Email);
        Assert.Null(grant.RevokedAt);

        var email = Assert.Single(await OutboxAsync(buyer, "OrderAccess"));
        Assert.Equal("Your Adriatic Presets", email.Subject);
        Assert.Contains($"https://app.luma.test/d/{grant.Token}", email.HtmlBody);
        Assert.Contains($"https://app.luma.test/d/{grant.Token}", email.PlainTextBody);

        await using var db = _fixture.CreateDbContext();
        var receipt = await new OrderReceiptReader(db).GetBySessionIdAsync(sessionId, None);
        Assert.Equal(grant.Token, receipt!.AccessToken);
        Assert.Equal(("Presets.zip", 18_350_080L), (receipt.FileName, receipt.FileSizeBytes));
    }

    [Fact]
    public async Task ARefund_TurnsTheLinkOff_SoTheDownloadPageAnswersGone()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);
        var sessionId = await CreatePendingOrderAsync(creator.CreatorId, product.Id, TestData.UniqueEmail("buyer"));
        await PayAsync(sessionId);
        var token = Assert.Single(await OrderGrantsAsync(sessionId)).Token;

        await RefundAsync(sessionId);

        var grant = Assert.Single(await OrderGrantsAsync(sessionId));
        Assert.NotNull(grant.RevokedAt);
        await using var db = _fixture.CreateDbContext();
        var get = await Assert.ThrowsAsync<GoneException>(() => Access(db).GetAsync(token, None));
        var download = await Assert.ThrowsAsync<GoneException>(() => Access(db).DownloadAsync(token, None));
        Assert.Equal(ProductAccessService.RevokedCode, get.Code);
        Assert.Equal(ProductAccessService.RevokedCode, download.Code);
        Assert.Equal(0, Assert.Single(await OrderGrantsAsync(sessionId)).DownloadCount);
        Assert.Null((await new OrderReceiptReader(db).GetBySessionIdAsync(sessionId, None))!.AccessToken);
    }

    [Fact]
    public async Task Downloads_HandOutTheFileByName_CountEveryOne_AndShowOnTheOrdersList()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId, fileName: "Adriatic Presets.zip");
        var sessionId = await CreatePendingOrderAsync(creator.CreatorId, product.Id, TestData.UniqueEmail("buyer"));
        await PayAsync(sessionId);
        var token = Assert.Single(await OrderGrantsAsync(sessionId)).Token;

        await using (var db = _fixture.CreateDbContext())
        {
            var access = await Access(db).GetAsync(token, None);
            Assert.Equal(("Adriatic Presets", "Adriatic Presets.zip", 18_350_080L, "order"),
                (access.ProductName, access.FileName, access.FileSizeBytes, access.Source));
            Assert.Equal("Test Creator", access.Brand.Name);
            Assert.NotNull(access.OrderNumber);
        }

        // Parallel downloads (several tabs, a retry): each one counts, none is lost.
        await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var db = _fixture.CreateDbContext();
            var download = await Access(db).DownloadAsync(token, None);
            Assert.Contains(product.BlobPath, download.Url);
        }));

        var grant = Assert.Single(await OrderGrantsAsync(sessionId));
        Assert.Equal(6, grant.DownloadCount);
        Assert.NotNull(grant.LastDownloadedAt);
        Assert.All(_storage.DownloadSasCalls, call =>
            Assert.Equal((product.BlobPath, "Adriatic Presets.zip", "application/zip", TimeSpan.FromMinutes(10)), call));

        await using (var db = _fixture.CreateDbContext())
        {
            var order = Assert.Single(await new OrderRepository(db).GetByCreatorSlugAsync(
                creator.Slug, creator.OwnerUserId, null, null, null, null, null, null, 10, None));
            Assert.Equal(6, order.DownloadCount);
            Assert.Equal(grant.LastDownloadedAt, order.LastDownloadedAt);
        }
    }

    [Fact]
    public async Task AReplacedFile_ReachesAnOldLink_AndTheOldBlobIsDeleted()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId);
        var sessionId = await CreatePendingOrderAsync(creator.CreatorId, product.Id, TestData.UniqueEmail("buyer"));
        await PayAsync(sessionId);
        var token = Assert.Single(await OrderGrantsAsync(sessionId)).Token;

        var newPath = $"ProductFile/{creator.CreatorId}/{Guid.NewGuid()}.pdf";
        var reference = await ConfirmedUploadAsync(creator.CreatorId, newPath, "Presets v2.pdf");
        await using (var db = _fixture.CreateDbContext())
        {
            await Products(db).UpdateAsync(creator.Slug, product.PublicId, creator.OwnerUserId,
                new UpdateProductRequestDto { Name = "Adriatic Presets", PriceCents = 2900, FileReference = reference }, None);
        }

        await using (var db = _fixture.CreateDbContext())
            await Access(db).DownloadAsync(token, None);

        Assert.Equal((newPath, "Presets v2.pdf", "application/pdf"), _storage.DownloadSasCalls.Select(c => (c.BlobPath, c.FileName, c.ContentType)).Single());
        Assert.Equal([product.BlobPath], _storage.DeleteCalls);
    }

    [Fact]
    public async Task AFileReference_ResolvesOnlyForItsCreator_OnceConfirmed_AndWhileItsBlobExists()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        var path = $"ProductFile/{creator.CreatorId}/{Guid.NewGuid()}.pdf";
        var reference = await ConfirmedUploadAsync(creator.CreatorId, path, "Guide.pdf");

        await using var db = _fixture.CreateDbContext();
        var files = new ProductFiles(db, _storage);
        Assert.Equal(("Guide.pdf", path), ((await files.ResolveAsync(creator.CreatorId, reference, None))!.FileName, (await files.ResolveAsync(creator.CreatorId, reference, None))!.BlobPath));
        Assert.Null(await files.ResolveAsync(other.CreatorId, reference, None));
        Assert.Null(await files.ResolveAsync(creator.CreatorId, Guid.NewGuid(), None));

        _storage.Blobs.Remove(path);
        Assert.Null(await files.ResolveAsync(creator.CreatorId, reference, None));
    }

    private ProductService Products(CreatorPlatformDbContext db) => new(
        new ProductRepository(db),
        new ProductsCreatorContextProvider(db),
        new ProductsUnitOfWork(db),
        new OrderContextProvider(db),
        new LandingPageContextProvider(db),
        new ProductFiles(db, _storage));

    /// <summary>A confirmed product-file upload of the creator (the Media confirm step's result), its blob stored.</summary>
    private async Task<Guid> ConfirmedUploadAsync(int creatorId, string blobPath, string fileName)
    {
        _storage.Put(blobPath, "application/pdf", "%PDF-1.7"u8.ToArray());
        await using var db = _fixture.CreateDbContext();
        var upload = BlobReference.CreatePending(creatorId, MediaUploadPurpose.ProductFile,
            $"https://test.blob.core.windows.net/product-files/{blobPath}", blobPath, "application/pdf", DateTimeOffset.UtcNow);
        upload.Confirm(8, DateTimeOffset.UtcNow, fileName);
        db.Add(upload);
        await db.SaveChangesAsync();
        return upload.PublicId;
    }

    // ---- Lead pages -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ANewSignUpOnALeadPage_GetsOneEmailWithItsLink_AndARepeatGetsNone()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId, priceCents: 0, fileName: "Guide.pdf");
        var page = await CreatePageAsync(creator.CreatorId, product.Id);
        var email = TestData.UniqueEmail("lead");

        await _data.CaptureAsync(creator.CreatorId, page, email);
        await _data.CaptureAsync(creator.CreatorId, page, email.ToUpperInvariant());

        var grant = Assert.Single(await CaptureGrantsAsync(page, email));
        var sent = Assert.Single(await OutboxAsync(email, LeadMagnetPurpose));
        Assert.Equal("Your free download: Adriatic Presets", sent.Subject);
        Assert.Contains($"https://app.luma.test/d/{grant.Token}", sent.HtmlBody);
        Assert.Contains("/unsubscribe/", sent.HtmlBody);

        await using var db = _fixture.CreateDbContext();
        Assert.Equal("capture", (await Access(db).GetAsync(grant.Token, None)).Source);
    }

    [Fact]
    public async Task ARepeatSignUp_ResendsTheSameLink_OnceThe24HoursArePast()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId, priceCents: 0);
        var page = await CreatePageAsync(creator.CreatorId, product.Id);
        var email = TestData.UniqueEmail("lead");
        await _data.CaptureAsync(creator.CreatorId, page, email);
        var token = Assert.Single(await CaptureGrantsAsync(page, email)).Token;

        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""
                UPDATE products.access_grants SET "LastSentAt" = now() - interval '25 hours' WHERE "Token" = {token}
                """);
        await _data.CaptureAsync(creator.CreatorId, page, email);
        await _data.CaptureAsync(creator.CreatorId, page, email);

        var sent = await OutboxAsync(email, LeadMagnetPurpose);
        Assert.Equal(2, sent.Count);
        Assert.All(sent, s => Assert.Contains($"/d/{token}", s.PlainTextBody));
        Assert.Single(await CaptureGrantsAsync(page, email));
    }

    [Fact]
    public async Task ParallelSignUpsOfTheSameEmail_SendOneEmail()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId, priceCents: 0);
        var page = await CreatePageAsync(creator.CreatorId, product.Id);
        var email = TestData.UniqueEmail("lead");

        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => _data.CaptureAsync(creator.CreatorId, page, email)));

        Assert.Single(await CaptureGrantsAsync(page, email));
        Assert.Single(await OutboxAsync(email, LeadMagnetPurpose));
    }

    [Fact]
    public async Task ASignUpOverTheContactLimit_SendsNothing_AndLeavesNoLink()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId, priceCents: 0);
        var page = await CreatePageAsync(creator.CreatorId, product.Id);
        await _data.SetContactsUsageAsync(creator.CreatorId, TestData.FreePlanContactLimit);
        var email = TestData.UniqueEmail("lead");

        await Assert.ThrowsAsync<ConflictException>(() => _data.CaptureAsync(creator.CreatorId, page, email));

        Assert.Empty(await CaptureGrantsAsync(page, email));
        Assert.Empty(await OutboxAsync(email, LeadMagnetPurpose));
    }

    [Fact]
    public async Task AnUnsubscribedEmail_GetsTheDocument_WithoutTheMarketingLine()
    {
        var creator = await _data.CreateCreatorAsync();
        var product = await CreateProductAsync(creator.CreatorId, priceCents: 0);
        var page = await CreatePageAsync(creator.CreatorId, product.Id);
        var email = TestData.UniqueEmail("lead");
        await _data.UnsubscribeAsync(creator.CreatorId, email);

        await _data.CaptureAsync(creator.CreatorId, page, email);

        var sent = Assert.Single(await OutboxAsync(email, LeadMagnetPurpose));
        Assert.DoesNotContain("Unsubscribe", sent.HtmlBody);
        Assert.DoesNotContain("/unsubscribe/", sent.PlainTextBody);
    }

    [Fact]
    public async Task ASignUpWithoutADocument_OrOnASalesPage_SendsNothing()
    {
        var creator = await _data.CreateCreatorAsync();
        var newsletter = await CreatePageAsync(creator.CreatorId, productId: null);
        var sales = await CreatePageAsync(creator.CreatorId, (await CreateProductAsync(creator.CreatorId)).Id, type: "Sales");
        var email = TestData.UniqueEmail("lead");

        await _data.CaptureAsync(creator.CreatorId, newsletter, email);
        await _data.CaptureAsync(creator.CreatorId, sales, email);

        Assert.Empty(await OutboxAsync(email, LeadMagnetPurpose));
        Assert.Empty(await CaptureGrantsAsync(newsletter, email));
        Assert.Empty(await CaptureGrantsAsync(sales, email));
    }

    // ---- The products list's sign-ups ----------------------------------------------------------------------------------

    [Fact]
    public async Task SignUps_CountByTheProductALeadPageGivesAway_InTheCreatorsLocalMonths()
    {
        var creator = await _data.CreateCreatorAsync();
        await using (var db = _fixture.CreateDbContext())
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
                VALUES ({creator.CreatorId}, 'Island Studio', '#2F8F9D', 'Pacific/Kiritimati', 'en', now(), now())
                """);
        var guide = await CreateProductAsync(creator.CreatorId, priceCents: 0);
        var paid = await CreateProductAsync(creator.CreatorId);
        var lead = await CreatePageAsync(creator.CreatorId, guide.Id);
        var archivedLead = await CreatePageAsync(creator.CreatorId, guide.Id, status: "Archived");
        var sales = await CreatePageAsync(creator.CreatorId, paid.Id, type: "Sales");

        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        // Kiritimati is UTC+14: 30 Sep 12:00 UTC is already 1 Oct there, so it is this month's.
        await CaptureAtAsync(lead, new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));
        await CaptureAtAsync(lead, new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero));
        await CaptureAtAsync(archivedLead, new DateTimeOffset(2026, 5, 5, 9, 0, 0, TimeSpan.Zero));     // May: the oldest month
        await CaptureAtAsync(lead, new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero));            // outside the six months
        await CaptureAtAsync(sales, new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero));          // not a lead page

        await using var read = _fixture.CreateDbContext();
        var signups = await new LandingPageContextProvider(read).GetProductSignupsAsync(creator.CreatorId, now, None);

        var counted = Assert.Single(signups);
        Assert.Equal(guide.Id, counted.Key);
        Assert.Equal(4, counted.Value.Total);
        Assert.Equal([1, 0, 0, 0, 0, 2], counted.Value.Trend);
    }

    private async Task CaptureAtAsync(int landingPageId, DateTimeOffset capturedAt)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.email_captures ("Id", "LandingPageId", "Email", "CapturedAt")
            VALUES ({Guid.NewGuid()}, {landingPageId}, {TestData.UniqueEmail("lead")}, {capturedAt})
            """);
    }

    // ---- Schema and migration ---------------------------------------------------------------------------------------

    [Fact]
    public async Task TheDatabase_RefusesAnActiveProductWithoutAFile_AndAGrantWithTwoSources()
    {
        var creator = await _data.CreateCreatorAsync();
        await using var db = _fixture.CreateDbContext();

        var product = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creator.CreatorId}, 'No file', 1000, 'Active', now(), now())
            """));
        Assert.Equal("CK_products_Active_Has_File", product.ConstraintName);

        var seeded = await CreateProductAsync(creator.CreatorId);
        var grant = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync($"""
            INSERT INTO products.access_grants ("Token", "CreatorId", "ProductId", "Email", "CreatedAt", "DownloadCount")
            VALUES ({Guid.NewGuid().ToString("N")}, {creator.CreatorId}, {seeded.Id}, 'x@example.test', now(), 0)
            """));
        Assert.Equal("CK_access_grants_One_Source", grant.ConstraintName);
    }

    [Fact]
    public async Task TheMigrationStep_MakesActiveProductsWithoutAFileDrafts_AndIsIdempotent()
    {
        var creator = await _data.CreateCreatorAsync();
        var withFile = await CreateProductAsync(creator.CreatorId);

        await using var db = _fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // The state before the migration: Active products had no file. Rolled back with the transaction.
        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE products.products DROP CONSTRAINT "CK_products_Active_Has_File" """);
        var withoutFile = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creator.CreatorId}, 'Old product', 1000, 'Active', now(), now())
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();

        for (var run = 0; run < 2; run++)
            await db.Database.ExecuteSqlRawAsync(DeliverProductFilesThroughAccessLinks.DraftActiveProductsWithoutFileSql);

        var statuses = await db.Database.SqlQuery<string>($"""
            SELECT "Status" AS "Value" FROM products.products WHERE "Id" IN ({withFile.Id}, {withoutFile}) ORDER BY "Id"
            """).ToListAsync();
        Assert.Equal(["Active", "Draft"], statuses);

        await transaction.RollbackAsync();
    }

    private sealed class NoHomeSummaryCache : IHomeSummaryCache
    {
        public ValueTask<HomeSummaryDto?> GetAsync(int creatorId, CancellationToken ct) => ValueTask.FromResult<HomeSummaryDto?>(null);
        public Task SetAsync(int creatorId, HomeSummaryDto value, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(int creatorId, CancellationToken ct) => Task.CompletedTask;
    }
}
