using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Integration.Tests.Creators;

/// <summary>The race path: when two requests pass CreatorService's pre-checks at once, the unique indexes decide —
/// and the resulting 409 must carry the same codes as the pre-checks.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CreatorConflictCodesTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public CreatorConflictCodesTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<ConflictException> InsertCreatorAsync(int ownerUserId, string slug)
    {
        await using var db = _fixture.CreateDbContext();
        var creator = Creator.Create(
            ownerUserId, "Racing Creator", slug, Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect,
            DateTimeOffset.UtcNow);
        await new CreatorRepository(db).AddAsync(creator, CancellationToken.None);

        return await Assert.ThrowsAsync<ConflictException>(
            () => new CreatorsUnitOfWork(db).SaveChangesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DuplicateSlug_RejectedByTheIndex_HasTheSlugTakenCode()
    {
        var existing = await _data.CreateCreatorAsync();
        var newOwner = await _data.CreateUserAsync();

        var exception = await InsertCreatorAsync(newOwner, existing.Slug);

        Assert.Equal(("CREATOR_SLUG_TAKEN", "This creator URL is already taken."), (exception.Code, exception.Message));
    }

    [Fact]
    public async Task SecondWorkspaceForTheOwner_RejectedByTheIndex_HasTheAlreadyExistsCode()
    {
        var existing = await _data.CreateCreatorAsync();

        var exception = await InsertCreatorAsync(existing.OwnerUserId, $"c-{Guid.NewGuid():N}"[..30]);

        Assert.Equal(("CREATOR_ALREADY_EXISTS", "You already have a creator workspace."), (exception.Code, exception.Message));
    }
}
