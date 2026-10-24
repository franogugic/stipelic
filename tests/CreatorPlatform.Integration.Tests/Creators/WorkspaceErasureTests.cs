using CreatorPlatform.Analytics.Infrastructure.Workspaces;
using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Infrastructure.Workspaces;
using CreatorPlatform.Email.Infrastructure.Workspaces;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.LandingPages.Infrastructure.Workspaces;
using CreatorPlatform.Marketing.Infrastructure.Workspaces;
using CreatorPlatform.Media.Infrastructure.Workspaces;
using CreatorPlatform.Orders.Infrastructure.Workspaces;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Products.Infrastructure.Workspaces;
using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Creators;

/// <summary>
/// DEL-1 against the real schema: finishing a disabled workspace's deletion empties every table of its data, keeps the
/// paid and refunded orders anonymised (buyer email and country kept, name gone), leaves product and creator tombstones,
/// frees the slug, deletes the files after the commit, and is a no-op the second time. Another creator is untouched.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WorkspaceErasureTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly FakeCheckoutSessionService _sessions = new();
    private readonly RecordingWorkspaceFiles _files = new();

    public WorkspaceErasureTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<WorkspaceErasureOutcome> FinishAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return await Erasure(db).FinishAsync(creatorId, DateTimeOffset.UtcNow, None);
    }

    private WorkspaceErasure Erasure(CreatorPlatformDbContext db) => new(
        db,
        [
            // Deliberately out of order: the erasure sorts them.
            new CreatorWorkspaceDataPurger(db),
            new ProductsWorkspaceDataPurger(db),
            new EmailWorkspaceDataPurger(db),
            new LandingPagesWorkspaceDataPurger(db),
            new MarketingWorkspaceDataPurger(db),
            new MediaWorkspaceDataPurger(db),
            new OrdersWorkspaceDataPurger(db),
            new AnalyticsWorkspaceDataPurger(db),
            new AccessLinksWorkspaceDataPurger(db),
        ],
        _sessions,
        _files,
        NullLogger<WorkspaceErasure>.Instance);

    [Fact]
    public async Task ADeletedWorkspace_IsErased_KeptOrdersAnonymised_AndAnotherCreatorUntouched()
    {
        var workspace = await SeedWorkspaceAsync();
        var other = await SeedWorkspaceAsync();
        var otherBefore = await CountsAsync(other);
        await DisableAsync(workspace.Creator.CreatorId);

        Assert.Equal(WorkspaceErasureOutcome.Done, await FinishAsync(workspace.Creator.CreatorId));

        var counts = await CountsAsync(workspace);
        foreach (var (table, count) in counts)
        {
            var expected = table switch
            {
                "orders" => 2,                  // the paid and the refunded one
                "products" => 1,                // the sold one, as a tombstone
                "creator_subscriptions" => 1,   // billing history
                _ => 0
            };
            Assert.True(expected == count, $"{table}: expected {expected}, found {count}");
        }
        Assert.Equal([workspace.PendingSessionId], _sessions.ExpireCalls);
        Assert.Equal([workspace.Creator.CreatorId], _files.Calls);

        await using var db = _fixture.CreateDbContext();
        var orders = await db.Database.SqlQuery<KeptOrder>($"""
            SELECT "Status", "Email", "Name", "LandingPageId", "BuyerCountryCode", "AmountCents", "PlatformFeeCents",
                   "RefundedCents", "StripePaymentIntentId"
            FROM orders.orders WHERE "CreatorId" = {workspace.Creator.CreatorId} ORDER BY "Status"
            """).ToListAsync();
        Assert.Equal(
            [
                new KeptOrder("Paid", "paid@example.test", null, null, "HR", 2900, 290, 0, "pi_paid"),
                new KeptOrder("Refunded", "refunded@example.test", null, null, "SI", 2900, 290, 2900, "pi_refunded"),
            ],
            orders);

        var product = (await db.Database.SqlQuery<ProductTombstone>($"""
            SELECT "Name", "Description", "ThumbnailUrl", "Status", "FileBlobPath"
            FROM products.products WHERE "CreatorId" = {workspace.Creator.CreatorId}
            """).ToListAsync()).Single();
        Assert.Equal(new ProductTombstone("Sold course", null, null, "Archived", null), product);

        var creator = (await db.Database.SqlQuery<CreatorTombstone>($"""
            SELECT "Status", "Name", "Slug", "OwnerUserId", "StripeConnectAccountId", "StripeCustomerId",
                   "StripeConnectPayoutsEnabled", "StripeConnectPayoutsEnabledAt", "CountryCode",
                   "DeletedAt" IS NOT NULL AS "Deleted", "FilesDeletedAt" IS NOT NULL AS "FilesDeleted", "PublicId"
            FROM creators.creators WHERE "Id" = {workspace.Creator.CreatorId}
            """).ToListAsync()).Single();
        Assert.Equal(
            new CreatorTombstone("Disabled", "Deleted workspace", $"deleted-{creator.PublicId:N}", null,
                $"acct_erasure_{workspace.Creator.CreatorId}", $"cus_erasure_{workspace.Creator.CreatorId}", false, null, "HR",
                true, true, creator.PublicId),
            creator);

        Assert.Equal(otherBefore, await CountsAsync(other));
    }

    [Fact]
    public async Task ASecondRun_IsANoOp()
    {
        var workspace = await SeedWorkspaceAsync();
        await DisableAsync(workspace.Creator.CreatorId);
        await FinishAsync(workspace.Creator.CreatorId);
        var after = await CountsAsync(workspace);
        var deletedAt = await DeletedAtAsync(workspace.Creator.CreatorId);

        Assert.Equal(WorkspaceErasureOutcome.Done, await FinishAsync(workspace.Creator.CreatorId));

        Assert.Equal(after, await CountsAsync(workspace));
        Assert.Equal(deletedAt, await DeletedAtAsync(workspace.Creator.CreatorId));
        Assert.Single(_files.Calls);
        Assert.Single(_sessions.ExpireCalls);
    }

    [Fact]
    public async Task TheOwner_CanStartANewWorkspaceWithTheSameSlug()
    {
        var workspace = await SeedWorkspaceAsync();
        await DisableAsync(workspace.Creator.CreatorId);
        await FinishAsync(workspace.Creator.CreatorId);

        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creators ("PublicId", "OwnerUserId", "Name", "Slug", "Status", "DefaultCurrency", "CountryCode",
                                           "StripeConnectDetailsSubmitted", "StripeConnectChargesEnabled",
                                           "StripeConnectPayoutsEnabled", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {workspace.Creator.OwnerUserId}, 'Again', {workspace.Creator.Slug}, 'Active', 'Eur', 'HR',
                    false, false, false, {now}, {now})
            """);
    }

    [Fact]
    public async Task AWorkspaceThatIsNotDisabled_IsLeftAlone()
    {
        var workspace = await SeedWorkspaceAsync();
        var before = await CountsAsync(workspace);

        Assert.Equal(WorkspaceErasureOutcome.NotDeleted, await FinishAsync(workspace.Creator.CreatorId));

        Assert.Equal(before, await CountsAsync(workspace));
        Assert.Empty(_sessions.ExpireCalls);
        Assert.Empty(_files.Calls);
    }

    [Fact]
    public async Task ASessionPaidJustNow_HoldsTheErasure_UntilItsOrderIsSettled()
    {
        var workspace = await SeedWorkspaceAsync();
        await DisableAsync(workspace.Creator.CreatorId);
        var before = await CountsAsync(workspace);
        _sessions.ExpireOutcome = CheckoutSessionExpireOutcome.AlreadyCompleted;

        Assert.Equal(WorkspaceErasureOutcome.WaitingForSale, await FinishAsync(workspace.Creator.CreatorId));

        Assert.Equal(before, await CountsAsync(workspace));
        Assert.Null(await DeletedAtAsync(workspace.Creator.CreatorId));
        Assert.Empty(_files.Calls);
    }

    [Fact]
    public async Task ASaleStartedWhileDeleting_HoldsTheErasure()
    {
        var workspace = await SeedWorkspaceAsync();
        await DisableAsync(workspace.Creator.CreatorId);
        var before = await CountsAsync(workspace);
        // While Stripe expires the known session, a checkout that had already passed the gate opens another one.
        _sessions.OnExpire = async _ =>
        {
            _sessions.OnExpire = null;
            await InsertOrderAsync(workspace.Creator.CreatorId, workspace.SoldProductId, "Pending", $"cs_late_{Guid.NewGuid():N}");
        };

        Assert.Equal(WorkspaceErasureOutcome.WaitingForSale, await FinishAsync(workspace.Creator.CreatorId));

        Assert.Equal(before["orders"] + 1, (await CountsAsync(workspace))["orders"]);
        Assert.Null(await DeletedAtAsync(workspace.Creator.CreatorId));
    }

    [Fact]
    public async Task AStripeFailure_ErasesNothing()
    {
        var workspace = await SeedWorkspaceAsync();
        await DisableAsync(workspace.Creator.CreatorId);
        var before = await CountsAsync(workspace);
        _sessions.ExpireFailure = new HttpRequestException("Stripe is unreachable.");

        await Assert.ThrowsAsync<HttpRequestException>(() => FinishAsync(workspace.Creator.CreatorId));

        Assert.Equal(before, await CountsAsync(workspace));
        Assert.Null(await DeletedAtAsync(workspace.Creator.CreatorId));
    }

    [Fact]
    public async Task FilesThatFailToDelete_AreRetried_WithoutErasingTheDataAgain()
    {
        var workspace = await SeedWorkspaceAsync();
        await DisableAsync(workspace.Creator.CreatorId);
        _files.Failure = new InvalidOperationException("Blob storage is down.");

        Assert.Equal(WorkspaceErasureOutcome.FilesPending, await FinishAsync(workspace.Creator.CreatorId));
        Assert.NotNull(await DeletedAtAsync(workspace.Creator.CreatorId));
        Assert.False(await FilesDeletedAsync(workspace.Creator.CreatorId));

        _files.Failure = null;
        Assert.Equal(WorkspaceErasureOutcome.Done, await FinishAsync(workspace.Creator.CreatorId));

        Assert.True(await FilesDeletedAsync(workspace.Creator.CreatorId));
        Assert.Equal([workspace.Creator.CreatorId, workspace.Creator.CreatorId], _files.Calls);
        Assert.Single(_sessions.ExpireCalls);
    }

    // ---- Seeding --------------------------------------------------------------------------------------------------

    private sealed record SeededWorkspace(
        TestData.SeededCreator Creator, int SoldProductId, int LandingPageId, int CampaignId, int ResendId, Guid CaptureId,
        string PendingSessionId);

    /// <summary>A workspace with a row in every table that holds workspace data.</summary>
    private async Task<SeededWorkspace> SeedWorkspaceAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        var c = creator.CreatorId;
        var now = DateTimeOffset.UtcNow;
        await using var db = _fixture.CreateDbContext();

        async Task<int> Scalar(FormattableString sql) => (await db.Database.SqlQuery<int>(sql).ToListAsync()).Single();

        await db.Database.ExecuteSqlAsync($"""
            UPDATE creators.creators
            SET "StripeConnectAccountId" = {$"acct_erasure_{c}"}, "StripeCustomerId" = {$"cus_erasure_{c}"},
                "StripeConnectDetailsSubmitted" = true, "StripeConnectChargesEnabled" = true, "StripeConnectPayoutsEnabled" = true,
                "StripeConnectPayoutsEnabledAt" = {now}
            WHERE "Id" = {c};
            INSERT INTO creators.creator_settings ("CreatorId", "BrandName", "PrimaryColor", "Timezone", "SupportEmail", "CreatedAt", "UpdatedAt")
            VALUES ({c}, 'Brand', '#000000', 'Europe/Zagreb', 'support@example.test', {now}, {now});
            INSERT INTO creators.creator_members ("PublicId", "CreatorId", "UserId", "Role", "Status", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {c}, {creator.OwnerUserId}, 'Owner', 'Active', {now});
            INSERT INTO creators.creator_usage_counters ("CreatorId", "UsageKey", "UsedValue", "PeriodStart", "PeriodEnd", "CreatedAt", "UpdatedAt")
            VALUES ({c}, 'max_contacts', 1, '-infinity', '9999-12-31', {now}, {now});
            """);

        var soldProduct = await Scalar($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "Description", "PriceCents", "Status", "ThumbnailUrl",
                                           "FileBlobPath", "FileName", "FileSizeBytes", "FileContentType", "FileUploadedAt",
                                           "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {c}, 'Sold course', 'All about it', 2900, 'Active', 'https://img.test/sold.png',
                    {$"ProductFile/{c}/sold.pdf"}, 'Sold.pdf', 100, 'application/pdf', {now}, {now}, {now})
            RETURNING "Id" AS "Value"
            """);
        var unsoldProduct = await Scalar($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {c}, 'Never sold', 0, 'Draft', {now}, {now})
            RETURNING "Id" AS "Value"
            """);

        var page = await Scalar($"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "ProductId", "Title", "Slug", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {c}, {soldProduct}, 'Sale', {$"sale-{Guid.NewGuid():N}"[..20]}, 'Sales', 'Published', {now}, {now})
            RETURNING "Id" AS "Value"
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO landing_pages.landing_page_sections ("PublicId", "LandingPageId", "Type", "SortOrder", "ContentJson", "CreatedAt", "UpdatedAt", "Variant")
            VALUES ({Guid.NewGuid()}, {page}, 'Hero', 0, CAST({"{}"} AS jsonb), {now}, {now}, 'Default');
            INSERT INTO analytics.page_views ("Id", "LandingPageId", "VisitorId", "ViewedAt")
            VALUES ({Guid.NewGuid()}, {page}, {Guid.NewGuid()}, {now});
            INSERT INTO analytics.creator_view_totals ("CreatorId", "TotalViews") VALUES ({c}, 1);
            """);
        var capture = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.email_captures ("Id", "LandingPageId", "ProductId", "Email", "CapturedAt")
            VALUES ({capture}, {page}, {unsoldProduct}, 'lead@example.test', {now})
            """);

        var paid = await InsertOrderAsync(c, soldProduct, "Paid", $"cs_paid_{Guid.NewGuid():N}", page, "paid@example.test", "Paid Buyer", "HR", "pi_paid");
        await InsertOrderAsync(c, soldProduct, "Refunded", $"cs_ref_{Guid.NewGuid():N}", page, "refunded@example.test", "Refunded Buyer", "SI", "pi_refunded");
        var pendingSession = $"cs_pending_{Guid.NewGuid():N}";
        await InsertOrderAsync(c, soldProduct, "Pending", pendingSession, page);
        await InsertOrderAsync(c, soldProduct, "Failed", null, page);

        var grant = await Scalar($"""
            INSERT INTO products.access_grants ("Token", "CreatorId", "ProductId", "OrderId", "Email", "CreatedAt", "DownloadCount")
            VALUES ({Guid.NewGuid().ToString("N")}, {c}, {soldProduct}, {paid}, 'paid@example.test', {now}, 1)
            RETURNING "Id" AS "Value"
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO products.access_grants ("Token", "CreatorId", "ProductId", "EmailCaptureId", "Email", "CreatedAt", "DownloadCount")
            VALUES ({Guid.NewGuid().ToString("N")}, {c}, {unsoldProduct}, {capture}, 'lead@example.test', {now}, 0);
            INSERT INTO products.download_events ("GrantId", "CreatorId", "ProductId", "LandingPageId", "DownloadedAt")
            VALUES ({grant}, {c}, {soldProduct}, {page}, {now});
            """);

        var template = await Scalar($"""
            INSERT INTO marketing.email_templates ("PublicId", "CreatorId", "Name", "Subject", "BodyText", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {c}, 'Template', 'Hi', 'Body', 'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """);
        var campaignPublicId = Guid.NewGuid();
        var campaign = await Scalar($"""
            INSERT INTO marketing.campaigns ("PublicId", "CreatorId", "Subject", "BodyText", "AudienceType", "Status", "RecipientCount",
                                             "TemplateId", "LandingPageId", "CreatedAt", "UpdatedAt")
            VALUES ({campaignPublicId}, {c}, 'News', 'Body', 'LandingPage', 'Sent', 1, {template}, {page}, {now}, {now})
            RETURNING "Id" AS "Value"
            """);
        var resend = await Scalar($"""
            INSERT INTO marketing.campaigns ("PublicId", "CreatorId", "Subject", "BodyText", "AudienceType", "Status", "RecipientCount",
                                             "SourceCampaignId", "ResendRound", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {c}, 'News again', 'Body', 'CampaignNonOpeners', 'Sent', 1, {campaign}, 1, {now}, {now})
            RETURNING "Id" AS "Value"
            """);
        var recipient = await Scalar($"""
            INSERT INTO marketing.campaign_recipients ("CampaignId", "Email", "CreatedAt") VALUES ({campaign}, 'lead@example.test', {now})
            RETURNING "Id" AS "Value"
            """);
        var link = await Scalar($"""
            INSERT INTO marketing.campaign_links ("CampaignId", "Position", "Url", "Kind", "Label", "CreatedAt")
            VALUES ({campaign}, 0, 'https://example.test', 'Button', 'Go', {now})
            RETURNING "Id" AS "Value"
            """);
        var import = await Scalar($"""
            INSERT INTO marketing.contact_imports ("PublicId", "CreatorId", "UserId", "FileName", "RowCount", "ImportedCount",
                                                   "ExistingCount", "DuplicateCount", "UnsubscribedCount", "InvalidCount",
                                                   "ConsentVersion", "ConsentAttestedAt", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {c}, {creator.OwnerUserId}, 'contacts.csv', 1, 1, 0, 0, 0, 0, 'v1', {now}, {now})
            RETURNING "Id" AS "Value"
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.campaign_link_clicks ("LinkId", "RecipientId", "FirstClickedAt", "LastClickedAt", "ClickCount")
            VALUES ({link}, {recipient}, {now}, {now}, 1);
            INSERT INTO marketing.contact_summaries ("CreatorId", "Email", "FirstCapturedAt", "LastCapturedAt", "SourceLandingPageIds", "ImportId")
            VALUES ({c}, 'lead@example.test', {now}, {now}, ARRAY[{page}], {import});
            INSERT INTO marketing.unsubscribes ("CreatorId", "Email", "Source", "UnsubscribedAt") VALUES ({c}, 'gone@example.test', 'Link', {now});
            INSERT INTO media.blob_references ("PublicId", "CreatorId", "Purpose", "BlobUrl", "BlobPath", "ContentType", "Status", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {c}, 'ProductFile', {$"https://blob.test/ProductFile/{c}/sold.pdf"}, {$"ProductFile/{c}/sold.pdf"}, 'application/pdf', 'Confirmed', {now});
            INSERT INTO email.email_outbox_messages ("Id", "ToEmail", "Subject", "Status", "RetryCount", "NextAttemptAt", "CreatedAt",
                                                     "CorrelationKey", "Purpose", "CreatorId")
            VALUES ({Guid.NewGuid()}, 'paid@example.test', 'Your course', 'Sent', 0, {now}, {now}, 'order', 'OrderAccess', {c}),
                   ({Guid.NewGuid()}, 'lead@example.test', 'News', 'Pending', 0, {now}, {now}, {$"{campaignPublicId}:1"}, 'CampaignBroadcast', {c});
            """);

        return new SeededWorkspace(creator, soldProduct, page, campaign, resend, capture, pendingSession);
    }

    private async Task<int> InsertOrderAsync(
        int creatorId, int productId, string status, string? sessionId, int? landingPageId = null, string? email = null,
        string? name = null, string? country = null, string? paymentIntentId = null)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var refunded = status == "Refunded" ? 2900 : 0;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "LandingPageId", "Email", "Name", "BuyerCountryCode",
                                       "AmountCents", "Currency", "Status", "PlatformFeeBasisPoints", "PlatformFeeCents",
                                       "RefundedCents", "StripeCheckoutSessionId", "StripePaymentIntentId", "CreatedAt",
                                       "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {productId}, {landingPageId}, {email}, {name}, {country}, 2900, 'Eur', {status},
                    1000, 290, {refunded}, {sessionId}, {paymentIntentId}, {now},
                    {(status is "Paid" or "Refunded" ? now : null)}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task DisableAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""UPDATE creators.creators SET "Status" = 'Disabled' WHERE "Id" = {creatorId}""");
    }

    // ---- Reading ----------------------------------------------------------------------------------------------------

    private sealed record KeptOrder(
        string Status, string? Email, string? Name, int? LandingPageId, string? BuyerCountryCode, int AmountCents,
        int PlatformFeeCents, int RefundedCents, string? StripePaymentIntentId);

    private sealed record ProductTombstone(string Name, string? Description, string? ThumbnailUrl, string Status, string? FileBlobPath);

    private sealed record CreatorTombstone(
        string Status, string Name, string Slug, int? OwnerUserId, string? StripeConnectAccountId, string? StripeCustomerId,
        bool StripeConnectPayoutsEnabled, DateTimeOffset? StripeConnectPayoutsEnabledAt, string CountryCode, bool Deleted,
        bool FilesDeleted, Guid PublicId);

    /// <summary>The workspace's rows per table, by creator or by the seeded parent rows of tables without a creator.</summary>
    private async Task<Dictionary<string, int>> CountsAsync(SeededWorkspace w)
    {
        var c = w.Creator.CreatorId;
        await using var db = _fixture.CreateDbContext();
        var queries = new Dictionary<string, FormattableString>
        {
            ["email_outbox_messages"] = $"""SELECT COUNT(*)::int AS "Value" FROM email.email_outbox_messages WHERE "CreatorId" = {c}""",
            ["campaigns"] = $"""SELECT COUNT(*)::int AS "Value" FROM marketing.campaigns WHERE "CreatorId" = {c}""",
            ["campaign_recipients"] = $"""SELECT COUNT(*)::int AS "Value" FROM marketing.campaign_recipients WHERE "CampaignId" IN ({w.CampaignId}, {w.ResendId})""",
            ["campaign_links"] = $"""SELECT COUNT(*)::int AS "Value" FROM marketing.campaign_links WHERE "CampaignId" IN ({w.CampaignId}, {w.ResendId})""",
            ["campaign_link_clicks"] = $"""SELECT COUNT(*)::int AS "Value" FROM marketing.campaign_link_clicks k JOIN marketing.campaign_links l ON l."Id" = k."LinkId" WHERE l."CampaignId" = {w.CampaignId}""",
            ["email_templates"] = $"""SELECT COUNT(*)::int AS "Value" FROM marketing.email_templates WHERE "CreatorId" = {c}""",
            ["contact_summaries"] = $"""SELECT COUNT(*)::int AS "Value" FROM marketing.contact_summaries WHERE "CreatorId" = {c}""",
            ["contact_imports"] = $"""SELECT COUNT(*)::int AS "Value" FROM marketing.contact_imports WHERE "CreatorId" = {c}""",
            ["unsubscribes"] = $"""SELECT COUNT(*)::int AS "Value" FROM marketing.unsubscribes WHERE "CreatorId" = {c}""",
            ["access_grants"] = $"""SELECT COUNT(*)::int AS "Value" FROM products.access_grants WHERE "CreatorId" = {c}""",
            ["download_events"] = $"""SELECT COUNT(*)::int AS "Value" FROM products.download_events WHERE "CreatorId" = {c}""",
            ["page_views"] = $"""SELECT COUNT(*)::int AS "Value" FROM analytics.page_views WHERE "LandingPageId" = {w.LandingPageId}""",
            ["email_captures"] = $"""SELECT COUNT(*)::int AS "Value" FROM analytics.email_captures WHERE "Id" = {w.CaptureId}""",
            ["creator_view_totals"] = $"""SELECT COUNT(*)::int AS "Value" FROM analytics.creator_view_totals WHERE "CreatorId" = {c}""",
            ["orders"] = $"""SELECT COUNT(*)::int AS "Value" FROM orders.orders WHERE "CreatorId" = {c}""",
            ["landing_pages"] = $"""SELECT COUNT(*)::int AS "Value" FROM landing_pages.landing_pages WHERE "CreatorId" = {c}""",
            ["landing_page_sections"] = $"""SELECT COUNT(*)::int AS "Value" FROM landing_pages.landing_page_sections WHERE "LandingPageId" = {w.LandingPageId}""",
            ["products"] = $"""SELECT COUNT(*)::int AS "Value" FROM products.products WHERE "CreatorId" = {c}""",
            ["blob_references"] = $"""SELECT COUNT(*)::int AS "Value" FROM media.blob_references WHERE "CreatorId" = {c}""",
            ["creator_settings"] = $"""SELECT COUNT(*)::int AS "Value" FROM creators.creator_settings WHERE "CreatorId" = {c}""",
            ["creator_members"] = $"""SELECT COUNT(*)::int AS "Value" FROM creators.creator_members WHERE "CreatorId" = {c}""",
            ["creator_usage_counters"] = $"""SELECT COUNT(*)::int AS "Value" FROM creators.creator_usage_counters WHERE "CreatorId" = {c}""",
            ["creator_subscriptions"] = $"""SELECT COUNT(*)::int AS "Value" FROM creators.creator_subscriptions WHERE "CreatorId" = {c}""",
        };

        var counts = new Dictionary<string, int>();
        foreach (var (table, sql) in queries)
            counts[table] = (await db.Database.SqlQuery<int>(sql).ToListAsync()).Single();
        return counts;
    }

    private async Task<DateTimeOffset?> DeletedAtAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<DateTimeOffset?>($"""
            SELECT "DeletedAt" AS "Value" FROM creators.creators WHERE "Id" = {creatorId}
            """).ToListAsync()).Single();
    }

    private async Task<bool> FilesDeletedAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Database.SqlQuery<bool>($"""
            SELECT "FilesDeletedAt" IS NOT NULL AS "Value" FROM creators.creators WHERE "Id" = {creatorId}
            """).ToListAsync()).Single();
    }

    private sealed class RecordingWorkspaceFiles : IWorkspaceFiles
    {
        public Exception? Failure { get; set; }

        public List<int> Calls { get; } = [];

        public Task DeleteAllAsync(int creatorId, CancellationToken ct)
        {
            Calls.Add(creatorId);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
