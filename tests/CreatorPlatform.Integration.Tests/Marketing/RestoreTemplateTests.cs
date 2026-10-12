using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Marketing.Application.Dtos;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Exceptions;
using MarketingCreatorContextProvider = CreatorPlatform.Marketing.Infrastructure.Services.CreatorContextProvider;

namespace CreatorPlatform.Integration.Tests.Marketing;

/// <summary>An archived email template can be brought back, by its own workspace only.</summary>
[Collection(PostgresCollection.Name)]
public sealed class RestoreTemplateTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public RestoreTemplateTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<T> WithServiceAsync<T>(Func<EmailTemplateService, Task<T>> act)
    {
        await using var db = _fixture.CreateDbContext();
        return await act(new EmailTemplateService(
            new MarketingCreatorContextProvider(db), new EmailTemplateRepository(db), new MarketingUnitOfWork(db)));
    }

    private Task<EmailTemplateDto> CreateArchivedAsync(TestData.SeededCreator creator) =>
        WithServiceAsync(async service =>
        {
            var created = await service.CreateAsync(creator.Slug, creator.OwnerUserId, new SaveEmailTemplateRequestDto
            {
                Name = "Welcome",
                Subject = "Hello",
                BodyText = "Thanks for joining.",
            }, CancellationToken.None);
            return await service.ArchiveAsync(creator.Slug, creator.OwnerUserId, created.PublicId, CancellationToken.None);
        });

    [Fact]
    public async Task Restore_MakesTheTemplateActiveAgain_AndIsIdempotent()
    {
        var creator = await _data.CreateCreatorAsync();
        var archived = await CreateArchivedAsync(creator);
        Assert.Equal("Archived", archived.Status);

        var restored = await WithServiceAsync(s => s.RestoreAsync(creator.Slug, creator.OwnerUserId, archived.PublicId, CancellationToken.None));
        var again = await WithServiceAsync(s => s.RestoreAsync(creator.Slug, creator.OwnerUserId, archived.PublicId, CancellationToken.None));
        var reloaded = await WithServiceAsync(s => s.GetAsync(creator.Slug, creator.OwnerUserId, archived.PublicId, CancellationToken.None));

        Assert.Equal(("Active", "Active", "Active"), (restored.Status, again.Status, reloaded.Status));
    }

    [Fact]
    public async Task Restore_AnotherWorkspacesTemplate_Is404_AndLeavesItArchived()
    {
        var owner = await _data.CreateCreatorAsync();
        var other = await _data.CreateCreatorAsync();
        var archived = await CreateArchivedAsync(owner);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            WithServiceAsync(s => s.RestoreAsync(other.Slug, other.OwnerUserId, archived.PublicId, CancellationToken.None)));

        var reloaded = await WithServiceAsync(s => s.GetAsync(owner.Slug, owner.OwnerUserId, archived.PublicId, CancellationToken.None));
        Assert.Equal("Archived", reloaded.Status);
    }
}
