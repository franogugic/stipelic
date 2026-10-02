using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Persistence;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Creators.Application.Interfaces;
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

    /// <summary>Seeded by the migrations: the paid "basic" plan (has a Stripe price id).</summary>
    public const int BasicPlanId = 2;

    /// <summary>The free plan's max_contacts limit, as seeded.</summary>
    public const int FreePlanContactLimit = 500;

    private const string MaxContactsKey = "max_contacts";

    private readonly PostgresFixture _fixture;

    public TestData(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public sealed record SeededCreator(int CreatorId, int OwnerUserId, string Slug);

    public static string UniqueEmail(string prefix = "contact") => $"{prefix}-{Guid.NewGuid():N}@example.test";

    /// <summary>A verified user without a workspace.</summary>
    public async Task<int> CreateUserAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        return await ScalarAsync(db, $"""
            INSERT INTO auth.users ("PublicId", "Email", "PasswordHash", "FirstName", "LastName", "EmailVerifiedAt",
                                    "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {UniqueEmail("user")}, 'not-a-real-hash', 'Test', 'User', {now},
                    'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """);
    }

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

    /// <summary>A workspace that picked a paid plan and has not paid yet: creator and subscription both
    /// PendingPayment, optionally with a stored Checkout session.</summary>
    public async Task<SeededCreator> CreatePendingCreatorAsync(string? checkoutSessionId, int planId = BasicPlanId)
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
            VALUES ({Guid.NewGuid()}, {userId}, 'Pending Creator', {slug}, 'PendingPayment', 'Eur',
                    'HR', 'StripeConnect', false, false, false, {now}, {now})
            RETURNING "Id" AS "Value"
            """);

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_subscriptions ("CreatorId", "PlanId", "Status", "BillingInterval", "Provider",
                                                        "CancelAtPeriodEnd", "CheckoutSessionId", "CreatedAt",
                                                        "UpdatedAt")
            VALUES ({creatorId}, {planId}, 'PendingPayment', 'Monthly', 'Internal', false, {checkoutSessionId},
                    {now}, {now})
            """);

        return new SeededCreator(creatorId, userId, slug);
    }

    public sealed record SubscriptionRow(
        int Id,
        string PlanCode,
        string Status,
        string Provider,
        DateTimeOffset? CancelledAt,
        string? CheckoutSessionId,
        string? ProviderSubscriptionId,
        DateTimeOffset? CurrentPeriodStart,
        DateTimeOffset? CurrentPeriodEnd,
        DateTimeOffset? ProviderEventAt,
        bool CancelAtPeriodEnd);

    /// <summary>Every subscription of the creator, oldest first.</summary>
    public async Task<List<SubscriptionRow>> GetSubscriptionsAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();

        return await db.Database.SqlQuery<SubscriptionRow>($"""
            SELECT s."Id" AS "Id", p."Code" AS "PlanCode", s."Status" AS "Status", s."Provider" AS "Provider",
                   s."CancelledAt" AS "CancelledAt", s."CheckoutSessionId" AS "CheckoutSessionId",
                   s."ProviderSubscriptionId" AS "ProviderSubscriptionId",
                   s."CurrentPeriodStart" AS "CurrentPeriodStart", s."CurrentPeriodEnd" AS "CurrentPeriodEnd",
                   s."ProviderEventAt" AS "ProviderEventAt", s."CancelAtPeriodEnd" AS "CancelAtPeriodEnd"
            FROM creators.creator_subscriptions s
            JOIN creators.creator_plans p ON p."Id" = s."PlanId"
            WHERE s."CreatorId" = {creatorId}
            ORDER BY s."Id"
            """).ToListAsync();
    }

    public async Task<string> GetCreatorStatusAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();

        return (await db.Database.SqlQuery<string>($"""
            SELECT "Status" AS "Value" FROM creators.creators WHERE "Id" = {creatorId}
            """).ToListAsync()).Single();
    }

    public async Task<List<string>> GetWebhookFailureMessagesAsync(string eventId)
    {
        await using var db = _fixture.CreateDbContext();

        return await db.Database.SqlQuery<string>($"""
            SELECT "ErrorMessage" AS "Value" FROM payments.webhook_failures WHERE "EventId" = {eventId}
            """).ToListAsync();
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

    /// <summary>The creator's all-time max_contacts usage exactly as the app reads it (0 when no counter row).</summary>
    public async Task<int> GetContactsUsageAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();

        return await new CreatorUsageService(db).GetUsedAsync(
            creatorId, MaxContactsKey, UsagePeriod.AllTime, CancellationToken.None);
    }

    /// <summary>Number of max_contacts counter rows of the creator, whatever their period.</summary>
    public async Task<int> CountContactsUsageRowsAsync(int creatorId)
    {
        await using var db = _fixture.CreateDbContext();

        return await ScalarAsync(db, $"""
            SELECT COUNT(*)::int AS "Value" FROM creators.creator_usage_counters
            WHERE "CreatorId" = {creatorId} AND "UsageKey" = 'max_contacts'
            """);
    }

    /// <summary>Forces the creator's all-time max_contacts counter (the row the app reads) to
    /// <paramref name="usedValue"/>, creating it if missing.</summary>
    public async Task SetContactsUsageAsync(int creatorId, int usedValue)
    {
        await using var db = _fixture.CreateDbContext();
        var (periodStart, periodEnd) = UsagePeriodResolver.Resolve(UsagePeriod.AllTime, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;

        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_usage_counters
                ("CreatorId", "UsageKey", "UsedValue", "PeriodStart", "PeriodEnd", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, 'max_contacts', {usedValue}, {periodStart}, {periodEnd}, {now}, {now})
            ON CONFLICT ("CreatorId", "UsageKey", "PeriodStart", "PeriodEnd")
            DO UPDATE SET "UsedValue" = {usedValue}, "UpdatedAt" = {now}
            """);
    }

    private static async Task<int> ScalarAsync(CreatorPlatformDbContext db, FormattableString sql)
        => (await db.Database.SqlQuery<int>(sql).ToListAsync()).Single();
}
