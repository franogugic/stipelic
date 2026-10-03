using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Persistence;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using AnalyticsCreatorContextProvider = CreatorPlatform.Analytics.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Analytics;

/// <summary>The page analytics' captures list: newest first, bounded by ?limit= (default 20, at most 100).</summary>
[Collection(PostgresCollection.Name)]
public sealed class CapturesListTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public CapturesListTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<List<EmailCaptureResponseDto>> ListAsync(int landingPageId, int limit)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new EmailCaptureService(
            new EmailCaptureRepository(db),
            new AnalyticsCreatorContextProvider(db),
            new CreatorUsageService(db),
            new AnalyticsUnitOfWork(db));
        return await service.ListCapturesAsync(landingPageId, limit, CancellationToken.None);
    }

    /// <summary>Captures one minute apart: capture n (1-based) is at Start + n minutes, email "n@…".</summary>
    private async Task SeedCapturesAsync(int landingPageId, int count, string domain)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO analytics.email_captures ("Id", "LandingPageId", "Email", "CapturedAt")
            SELECT gen_random_uuid(), {landingPageId}, n || '@' || {domain}, {Start} + n * interval '1 minute'
            FROM generate_series(1, {count}) AS n
            """);
    }

    [Fact]
    public async Task DefaultsTo20_NewestFirst_AndOnlyThisPage()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        var otherPage = await _data.CreateLandingPageAsync(creator.CreatorId);
        await SeedCapturesAsync(page, 25, "mine.test");
        await SeedCapturesAsync(otherPage, 5, "other.test");

        var captures = await ListAsync(page, 0);

        Assert.Equal(20, captures.Count);
        Assert.Equal(Enumerable.Range(6, 20).Reverse().Select(n => $"{n}@mine.test"), captures.Select(c => c.Email));
        Assert.Equal(captures.OrderByDescending(c => c.CapturedAt).Select(c => c.Email), captures.Select(c => c.Email));
    }

    [Fact]
    public async Task HonoursTheLimit_AndCapsItAt100()
    {
        var creator = await _data.CreateCreatorAsync();
        var page = await _data.CreateLandingPageAsync(creator.CreatorId);
        await SeedCapturesAsync(page, 105, "many.test");

        var eight = await ListAsync(page, 8);
        var capped = await ListAsync(page, 500);

        Assert.Equal(Enumerable.Range(98, 8).Reverse().Select(n => $"{n}@many.test"), eight.Select(c => c.Email));
        Assert.Equal(100, capped.Count);
        Assert.Equal("105@many.test", capped[0].Email);
    }
}
