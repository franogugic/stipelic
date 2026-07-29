using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Application.Services;
using CreatorPlatform.LandingPages.Domain.LandingPages;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.LandingPages;

public class LandingPageRestoreTests
{
    private const string CreatorSlug = "acme";
    private const int OwnerUserId = 1;

    private static (LandingPageService Service, FakeLandingPageRepository Repository) BuildService(CreatorContext context)
    {
        var repository = new FakeLandingPageRepository();
        var sectionRepository = new FakeLandingPageSectionRepository();
        var contextProvider = new FakeLandingPagesCreatorContextProvider { Context = context };
        var unitOfWork = new FakeLandingPagesUnitOfWork();

        var service = new LandingPageService(repository, sectionRepository, contextProvider, unitOfWork);

        return (service, repository);
    }

    private static LandingPage CreateArchivedPage(int creatorId)
    {
        var page = LandingPage.Create(creatorId, productId: 1, "Title", "slug", LandingPageType.LeadGen, DateTimeOffset.UtcNow);
        page.Archive(DateTimeOffset.UtcNow);
        return page;
    }

    [Fact]
    public async Task RestoreAsync_ArchivedPage_WithRoomUnderLimit_RestoresToDraft()
    {
        var context = new CreatorContext(1, 5, 1);
        var (service, repository) = BuildService(context);
        var page = CreateArchivedPage(context.CreatorId);
        repository.PageForUpdate = page;

        var result = await service.RestoreAsync(CreatorSlug, page.PublicId, OwnerUserId, CancellationToken.None);

        Assert.Equal("Draft", result.Status);
        Assert.Equal(LandingPageStatus.Draft, page.Status);
    }

    [Fact]
    public async Task RestoreAsync_LimitFull_ThrowsConflictAndLeavesStatusUnchanged()
    {
        var context = new CreatorContext(1, 1, 1);
        var (service, repository) = BuildService(context);
        var page = CreateArchivedPage(context.CreatorId);
        repository.PageForUpdate = page;

        await Assert.ThrowsAsync<ConflictException>(
            () => service.RestoreAsync(CreatorSlug, page.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal(LandingPageStatus.Archived, page.Status);
    }

    [Fact]
    public async Task RestoreAsync_NonArchivedPage_Throws()
    {
        var context = new CreatorContext(1, 5, 0);
        var (service, repository) = BuildService(context);
        var page = LandingPage.Create(context.CreatorId, productId: 1, "Title", "slug", LandingPageType.LeadGen, DateTimeOffset.UtcNow);
        repository.PageForUpdate = page;

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.RestoreAsync(CreatorSlug, page.PublicId, OwnerUserId, CancellationToken.None));
    }

    [Fact]
    public async Task RestoreAsync_SlugTakenByAnotherPage_ThrowsConflict()
    {
        var context = new CreatorContext(1, 5, 1);
        var (service, repository) = BuildService(context);
        var page = CreateArchivedPage(context.CreatorId);
        repository.PageForUpdate = page;
        repository.SlugExists = true;

        await Assert.ThrowsAsync<ConflictException>(
            () => service.RestoreAsync(CreatorSlug, page.PublicId, OwnerUserId, CancellationToken.None));

        Assert.Equal(LandingPageStatus.Archived, page.Status);
    }

    [Fact]
    public async Task ListAsync_IncludeArchivedFalse_OmitsArchivedPages()
    {
        var context = new CreatorContext(1, 5, 1);
        var (service, repository) = BuildService(context);
        var archived = CreateArchivedPage(context.CreatorId);
        var active = LandingPage.Create(context.CreatorId, productId: 1, "Active", "active-slug", LandingPageType.LeadGen, DateTimeOffset.UtcNow);
        repository.Pages.Add(archived);
        repository.Pages.Add(active);

        var result = await service.ListAsync(CreatorSlug, OwnerUserId, includeArchived: false, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Active", result[0].Title);
    }

    [Fact]
    public async Task ListAsync_IncludeArchivedTrue_ReturnsArchivedToo()
    {
        var context = new CreatorContext(1, 5, 1);
        var (service, repository) = BuildService(context);
        var archived = CreateArchivedPage(context.CreatorId);
        var active = LandingPage.Create(context.CreatorId, productId: 1, "Active", "active-slug", LandingPageType.LeadGen, DateTimeOffset.UtcNow);
        repository.Pages.Add(archived);
        repository.Pages.Add(active);

        var result = await service.ListAsync(CreatorSlug, OwnerUserId, includeArchived: true, CancellationToken.None);

        Assert.Equal(2, result.Count);
    }
}
