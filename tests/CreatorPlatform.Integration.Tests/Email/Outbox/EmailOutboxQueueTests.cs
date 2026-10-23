using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Email.Outbox;

/// <summary>EMAIL-1: how campaign mail gets its place in the fair queue.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EmailOutboxQueueTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private IsolatedDatabase _database = null!;

    public EmailOutboxQueueTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync() => _database = await IsolatedDatabase.CreateAsync(_postgres);

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private async Task<List<(int? CreatorId, long? Position)>> PositionsAsync(IEnumerable<string> recipients)
    {
        var set = recipients.ToHashSet();
        await using var db = _database.CreateDbContext();
        return (await db.Set<EmailOutboxMessage>().AsNoTracking().ToListAsync())
            .Where(message => set.Contains(message.ToEmail))
            .OrderBy(message => message.QueuePosition)
            .Select(message => (message.CreatorId, message.QueuePosition))
            .ToList();
    }

    [Fact]
    public async Task TheFirstCampaign_StartsAtZero_AndANewCreatorStartsAtTheHead()
    {
        var first = await OutboxQueue.CampaignAsync(_database, creatorId: 7, "a", 3);
        var newcomer = await OutboxQueue.CampaignAsync(_database, creatorId: 8, "b", 2);

        Assert.Equal([(7, 0L), (7, 1L), (7, 2L)], (await PositionsAsync(first)).Select(p => (p.CreatorId!.Value, p.Position!.Value)));
        Assert.Equal([(8, 0L), (8, 1L)], (await PositionsAsync(newcomer)).Select(p => (p.CreatorId!.Value, p.Position!.Value)));
    }

    [Fact]
    public async Task ACreatorsNextCampaign_ContinuesAfterItsOwnWaitingMail()
    {
        await OutboxQueue.CampaignAsync(_database, creatorId: 7, "a1", 3);
        var next = await OutboxQueue.CampaignAsync(_database, creatorId: 7, "a2", 2);

        Assert.Equal([3L, 4L], (await PositionsAsync(next)).Select(p => p.Position!.Value));
    }

    [Fact]
    public async Task ANewcomer_StartsWhereTheQueueHasGotTo_NotAtZero()
    {
        var first = await OutboxQueue.CampaignAsync(_database, creatorId: 7, "a", 5);
        await using (var db = _database.CreateDbContext())
        {
            // The first two went out already.
            foreach (var message in db.Set<EmailOutboxMessage>().Where(row => row.ToEmail == first[0] || row.ToEmail == first[1]))
            {
                message.MarkAsProcessing(DateTimeOffset.UtcNow.AddMinutes(5));
                message.MarkAsSent(DateTimeOffset.UtcNow);
            }

            await db.SaveChangesAsync();
        }

        var newcomer = await OutboxQueue.CampaignAsync(_database, creatorId: 8, "b", 2);

        Assert.Equal([2L, 3L], (await PositionsAsync(newcomer)).Select(p => p.Position!.Value));
    }

    [Fact]
    public async Task TransactionalMail_HasNoPlaceInTheCampaignQueue()
    {
        var to = await OutboxQueue.TransactionalAsync(_database, "order");

        var message = await OutboxQueue.GetAsync(_database, to);

        Assert.Equal((null, null), (message.CreatorId, message.QueuePosition));
        Assert.True(message.IsTransactional);
    }
}
