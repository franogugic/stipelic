using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Options;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Domain.Campaigns;
using CreatorPlatform.Marketing.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.MoneyPath.Tests.Marketing;

/// <summary>Resolving a tracked link: an invalid token never reaches the database, the target comes from the
/// repository, and a failure to record still redirects.</summary>
public class ClickTrackingServiceTests
{
    private static readonly ClickTrackingTokenService Tokens =
        new(Options.Create(new MarketingOptions { ClickTrackingSecret = "click-secret-value-1234567890", ApiBaseUrl = "https://api.luma.test" }));

    private sealed class RecordingLinkRepository : ICampaignLinkRepository
    {
        public string? Target { get; init; }
        public bool FailToRecord { get; init; }
        public int Calls { get; private set; }
        public List<(int RecipientId, int LinkId)> Recorded { get; } = [];

        public Task AddRangeAsync(IEnumerable<CampaignLink> links, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<CampaignLink>> GetByCampaignIdAsync(int campaignId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<int, int>> GetLinkCountsAsync(IReadOnlyCollection<int> campaignIds, CancellationToken ct) => throw new NotSupportedException();

        public Task<string?> GetTargetUrlAsync(int recipientId, int linkId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Target);
        }

        public Task RecordClickAsync(int recipientId, int linkId, CancellationToken ct)
        {
            Calls++;
            if (FailToRecord)
                throw new InvalidOperationException("database down");
            Recorded.Add((recipientId, linkId));
            return Task.CompletedTask;
        }
    }

    private static ClickTrackingService Service(RecordingLinkRepository repository) =>
        new(Tokens, repository, NullLogger<ClickTrackingService>.Instance);

    [Fact]
    public async Task AnInvalidToken_IsNullWithoutTouchingTheDatabase()
    {
        var repository = new RecordingLinkRepository { Target = "https://shop.test" };

        Assert.Null(await Service(repository).ResolveClickAsync("forged.token", CancellationToken.None));
        Assert.Equal(0, repository.Calls);
    }

    [Fact]
    public async Task AValidToken_RedirectsToTheStoredUrl_AndRecordsTheClick()
    {
        var repository = new RecordingLinkRepository { Target = "https://shop.test/buy" };

        var target = await Service(repository).ResolveClickAsync(Tokens.Create(5, 9), CancellationToken.None);

        Assert.Equal("https://shop.test/buy", target);
        Assert.Equal([(5, 9)], repository.Recorded);
    }

    [Fact]
    public async Task ALinkThatIsNotTheRecipients_IsNull_AndRecordsNothing()
    {
        var repository = new RecordingLinkRepository { Target = null };

        Assert.Null(await Service(repository).ResolveClickAsync(Tokens.Create(5, 9), CancellationToken.None));
        Assert.Empty(repository.Recorded);
    }

    [Fact]
    public async Task AFailureToRecord_StillRedirects()
    {
        var repository = new RecordingLinkRepository { Target = "https://shop.test/buy", FailToRecord = true };

        Assert.Equal("https://shop.test/buy", await Service(repository).ResolveClickAsync(Tokens.Create(5, 9), CancellationToken.None));
    }
}
