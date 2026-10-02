using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Persistence;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AnalyticsCreatorContextProvider = CreatorPlatform.Analytics.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>Seeds rows with raw SQL (only the columns the schema requires) and unique values per call, so every
/// test owns its data. Captures go through the real <see cref="EmailCaptureService"/>, so the summary row and
/// the max_contacts usage counter are maintained exactly as in production.</summary>
public sealed class TestData
{
    /// <summary>Seeded by the migrations: the free plan (max_contacts = 500).</summary>
    public const int FreePlanId = 1;

    private readonly PostgresFixture _fixture;

    public TestData(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public sealed record SeededCreator(int CreatorId, int OwnerUserId, string Slug);

    public static string UniqueEmail(string prefix = "contact") => $"{prefix}-{Guid.NewGuid():N}@example.test";

    public async Task<SeededCreator> CreateCreatorAsync(int planId = FreePlanId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var slug = $"c-{Guid.NewGuid():N}"[..30];

        var userId = await ScalarAsync(db, $"""
            INSERT INTO auth.users ("PublicId", "Email", "PasswordHash", "FirstName", "LastName", "EmailVerifiedAt",
                                    "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {UniqueEmail("owner")}, 'not-a-real-hash', 'Test', 'Owner', {now},
                    'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """);

        var creatorId = await ScalarAsync(db, $"""
            INSERT INTO creators.creators ("PublicId", "OwnerUserId", "Name", "Slug", "Status", "DefaultCurrency",
                                           "CountryCode", "PayoutMode", "StripeConnectDetailsSubmitted",
                                           "StripeConnectChargesEnabled", "StripeConnectPayoutsEnabled",
                                           "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {userId}, 'Test Creator', {slug}, 'Active', 'Eur',
                    'HR', 'StripeConnect', false, false, false, {now}, {now})
            RETURNING "Id" AS "Value"
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_subscriptions ("CreatorId", "PlanId", "Status", "BillingInterval", "Provider",
                                                        "CancelAtPeriodEnd", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, {planId}, 'Active', 'Monthly', 'Internal', false, {now}, {now})
            """);

        return new SeededCreator(creatorId, userId, slug);
    }

    public async Task<int> CreateLandingPageAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        return await ScalarAsync(db, $"""
            INSERT INTO landing_pages.landing_pages ("PublicId", "CreatorId", "Title", "Slug", "Type", "Status",
                                                     "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Test Page', {$"p-{Guid.NewGuid():N}"}, 'LeadGen', 'Published',
                    {now}, {now})
            RETURNING "Id" AS "Value"
            """);
    }

    /// <summary>A public sign-up through the production capture path.</summary>
    public async Task CaptureAsync(int creatorId, int landingPageId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new EmailCaptureService(
            new EmailCaptureRepository(db),
            new AnalyticsCreatorContextProvider(db),
            new CreatorUsageService(db),
            new AnalyticsUnitOfWork(db));

        await service.CaptureAsync(landingPageId, null, creatorId, email, CancellationToken.None);
    }

    public async Task UnsubscribeAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.unsubscribes ("CreatorId", "Email", "Source", "UnsubscribedAt")
            VALUES ({creatorId}, {email}, 'Link', {DateTimeOffset.UtcNow})
            """);
    }

    /// <summary>A sent campaign to the All audience with one recipient row for <paramref name="email"/>.</summary>
    public async Task AddCampaignRecipientAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        var campaignId = await ScalarAsync(db, $"""
            INSERT INTO marketing.campaigns ("PublicId", "CreatorId", "Subject", "BodyText", "AudienceType", "Status",
                                             "RecipientCount", "QueuedAt", "UniqueOpenCount", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Hello', 'Body', 'All', 'Queued', 1, {now}, 0, {now}, {now})
            RETURNING "Id" AS "Value"
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.campaign_recipients ("CampaignId", "Email", "CreatedAt")
            VALUES ({campaignId}, {email}, {now})
            """);
    }

    public async Task<int> CountCapturesAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();

        return await ScalarAsync(db, $"""
            SELECT COUNT(*)::int AS "Value"
            FROM analytics.email_captures ec
            JOIN landing_pages.landing_pages lp ON lp."Id" = ec."LandingPageId"
            WHERE lp."CreatorId" = {creatorId} AND ec."Email" = {email}
            """);
    }

    public async Task<bool> SummaryExistsAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();

        return await ScalarAsync(db, $"""
            SELECT COUNT(*)::int AS "Value" FROM marketing.contact_summaries
            WHERE "CreatorId" = {creatorId} AND "Email" = {email}
            """) > 0;
    }

    public async Task<bool> UnsubscribeExistsAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();

        return await ScalarAsync(db, $"""
            SELECT COUNT(*)::int AS "Value" FROM marketing.unsubscribes
            WHERE "CreatorId" = {creatorId} AND "Email" = {email}
            """) > 0;
    }

    public async Task<int> CountCampaignRecipientsAsync(int creatorId, string email)
    {
        await using var db = _fixture.CreateDbContext();

        return await ScalarAsync(db, $"""
            SELECT COUNT(*)::int AS "Value"
            FROM marketing.campaign_recipients r
            JOIN marketing.campaigns c ON c."Id" = r."CampaignId"
            WHERE c."CreatorId" = {creatorId} AND r."Email" = {email}
            """);
    }

    /// <summary>The creator's all-time max_contacts usage (0 when no counter row exists yet).</summary>
    public async Task<int> GetContactsUsageAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();

        return await ScalarAsync(db, $"""
            SELECT COALESCE(SUM("UsedValue"), 0)::int AS "Value" FROM creators.creator_usage_counters
            WHERE "CreatorId" = {creatorId} AND "UsageKey" = 'max_contacts'
            """);
    }

    private static async Task<int> ScalarAsync(CreatorPlatformDbContext db, FormattableString sql)
        => (await db.Database.SqlQuery<int>(sql).ToListAsync()).Single();
}
