using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Integration.Tests.Infrastructure;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>The landing pages list's per-page Emails column: one grouped count over the requested pages.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CaptureCountsTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public CaptureCountsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<Dictionary<int, int>> CountAsync(params int[] landingPageIds)
    {
        await using var db = _fixture.CreateDbContext();
        return await new EmailCaptureRepository(db).GetCaptureCountsAsync(landingPageIds, CancellationToken.None);
    }

    [Fact]
    public async Task CountsCapturesPerRequestedPage_AndOmitsPagesWithout()
    {
        var creator = await _data.CreateCreatorAsync();
        var pageA = await _data.CreateLandingPageAsync(creator.CreatorId);
        var pageB = await _data.CreateLandingPageAsync(creator.CreatorId);
        var empty = await _data.CreateLandingPageAsync(creator.CreatorId);
        var shared = TestData.UniqueEmail();
        await _data.CaptureAsync(creator.CreatorId, pageA, shared);
        await _data.CaptureAsync(creator.CreatorId, pageA, TestData.UniqueEmail());
        await _data.CaptureAsync(creator.CreatorId, pageB, shared);

        var counts = await CountAsync(pageA, pageB, empty);

        Assert.Equal(2, counts[pageA]);
        Assert.Equal(1, counts[pageB]);
        Assert.False(counts.ContainsKey(empty));
    }

    [Fact]
    public async Task IgnoresPagesThatWereNotRequested()
    {
        var creator = await _data.CreateCreatorAsync();
        var mine = await _data.CreateLandingPageAsync(creator.CreatorId);
        var other = await _data.CreateLandingPageAsync((await _data.CreateCreatorAsync()).CreatorId);
        await _data.CaptureAsync(creator.CreatorId, mine, TestData.UniqueEmail());

        var counts = await CountAsync(mine);

        Assert.Equal([mine], counts.Keys);
        Assert.Empty(await CountAsync());
        Assert.False((await CountAsync(other)).ContainsKey(mine));
    }
}
