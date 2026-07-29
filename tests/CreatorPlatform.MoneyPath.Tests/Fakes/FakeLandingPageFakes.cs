using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Domain.LandingPages;

namespace CreatorPlatform.MoneyPath.Tests.Fakes;

public sealed class FakeLandingPagesCreatorContextProvider : ICreatorContextProvider
{
    public CreatorContext? Context { get; set; }

    public Task<CreatorContext?> GetBySlugForOwnerAsync(string slug, int ownerUserId, CancellationToken ct)
        => Task.FromResult(Context);

    public Task<int?> GetProductIdForCreatorAsync(int creatorId, Guid productPublicId, CancellationToken ct)
        => Task.FromResult<int?>(1);

    public Task<ProductInfo?> GetProductInfoAsync(int productId, CancellationToken ct)
        => Task.FromResult<ProductInfo?>(new ProductInfo("Product", 1000));
}

public sealed class FakeLandingPageRepository : ILandingPageRepository
{
    public LandingPage? PageForUpdate { get; set; }

    public bool SlugExists { get; set; }

    public List<LandingPage> Pages { get; } = [];

    public Task<List<LandingPage>> ListByCreatorIdAsync(int creatorId, bool includeArchived, CancellationToken ct)
        => Task.FromResult(Pages
            .Where(p => p.CreatorId == creatorId && (includeArchived || p.Status != LandingPageStatus.Archived))
            .ToList());

    public Task<LandingPage?> GetByPublicIdAndCreatorIdForUpdateAsync(Guid publicId, int creatorId, CancellationToken ct)
        => Task.FromResult(PageForUpdate);

    public Task<LandingPage?> GetByPublicIdAndCreatorIdAsync(Guid publicId, int creatorId, CancellationToken ct)
        => Task.FromResult(PageForUpdate);

    public Task<bool> SlugExistsForCreatorAsync(int creatorId, string slug, CancellationToken ct)
        => Task.FromResult(SlugExists);

    public Task<LandingPage?> GetPublishedBySlugAsync(string creatorSlug, string landingPageSlug, CancellationToken ct)
        => Task.FromResult(PageForUpdate);

    public Task AddAsync(LandingPage landingPage, CancellationToken ct) => Task.CompletedTask;
}

public sealed class FakeLandingPageSectionRepository : ILandingPageSectionRepository
{
    public Task<List<LandingPageSection>> ListByLandingPageIdAsync(int landingPageId, CancellationToken ct)
        => Task.FromResult(new List<LandingPageSection>());

    public Task<LandingPageSection?> GetByPublicIdAndLandingPageIdForUpdateAsync(Guid publicId, int landingPageId, CancellationToken ct)
        => Task.FromResult<LandingPageSection?>(null);

    public Task AddAsync(LandingPageSection section, CancellationToken ct) => Task.CompletedTask;

    public void Remove(LandingPageSection section)
    {
    }
}

public sealed class FakeLandingPagesUnitOfWork : ILandingPagesUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
