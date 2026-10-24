using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Email.Outbox;

/// <summary>The migration step that gives creator mail queued before it was recorded its creator: order emails through
/// the order, lead downloads through the product, campaign messages through the campaign; account mail keeps none.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OutboxCreatorBackfillTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public OutboxCreatorBackfillTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    [Fact]
    public async Task TheMigrationStep_LinksEachCreatorEmailToItsCreator_AndIsIdempotent()
    {
        var creator = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        var productId = (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creator.CreatorId}, 'Guide', 0, 'Draft', {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
        var orderPublicId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "AmountCents", "Currency", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({orderPublicId}, {creator.CreatorId}, {productId}, 0, 'Eur', 'Pending', {now}, {now})
            """);
        var campaignPublicId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO marketing.campaigns ("PublicId", "CreatorId", "Subject", "BodyText", "AudienceType", "Status",
                                             "RecipientCount", "CreatedAt", "UpdatedAt")
            VALUES ({campaignPublicId}, {other.CreatorId}, 'News', 'Body', 'All', 'Sent', 1, {now}, {now})
            """);

        var orderEmail = await InsertAsync(db, "OrderAccess", orderPublicId.ToString());
        var leadEmail = await InsertAsync(db, "LeadMagnetDelivery", $"lead:{Guid.NewGuid()}:{productId}");
        var campaignEmail = await InsertAsync(db, "CampaignBroadcast", $"{campaignPublicId}:7");
        var accountEmail = await InsertAsync(db, "EmailVerification", Guid.NewGuid().ToString());
        var unknownOrderEmail = await InsertAsync(db, "OrderAccess", Guid.NewGuid().ToString());

        for (var run = 0; run < 2; run++)
            await db.Database.ExecuteSqlRawAsync(LinkCreatorEmailsToTheirCreator.BackfillCreatorIdSql);

        Assert.Equal(creator.CreatorId, await CreatorIdAsync(db, orderEmail));
        Assert.Equal(creator.CreatorId, await CreatorIdAsync(db, leadEmail));
        Assert.Equal(other.CreatorId, await CreatorIdAsync(db, campaignEmail));
        Assert.Null(await CreatorIdAsync(db, accountEmail));
        Assert.Null(await CreatorIdAsync(db, unknownOrderEmail));
    }

    private static async Task<Guid> InsertAsync(DbContext db, string purpose, string correlationKey)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO email.email_outbox_messages ("Id", "ToEmail", "Subject", "Status", "RetryCount", "NextAttemptAt",
                                                     "CreatedAt", "CorrelationKey", "Purpose")
            VALUES ({id}, {TestData.UniqueEmail("to")}, 'Subject', 'Sent', 0, {now}, {now}, {correlationKey}, {purpose})
            """);
        return id;
    }

    private static async Task<int?> CreatorIdAsync(DbContext db, Guid messageId) =>
        (await db.Database.SqlQuery<int?>($"""
            SELECT "CreatorId" AS "Value" FROM email.email_outbox_messages WHERE "Id" = {messageId}
            """).ToListAsync()).Single();
}
