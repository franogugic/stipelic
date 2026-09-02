using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.MoneyPath.Tests.Orders;

public class OrderServiceTests
{
    private const string CreatorSlug = "acme";
    private const int OwnerUserId = 1;

    private static OrderDto BuildOrderDto(DateTimeOffset createdAt, Guid publicId) => new(
        publicId,
        "buyer@example.com",
        null,
        "Course",
        1000,
        Currency.Eur.ToString(),
        "Paid",
        createdAt,
        createdAt,
        100,
        900,
        null);

    private static (OrderService Service, FakeOrderListingRepository Repository) BuildService()
    {
        var repository = new FakeOrderListingRepository();
        var service = new OrderService(repository, new FakeOrderListingHomeSummaryCache());
        return (service, repository);
    }

    [Fact]
    public async Task ListAsync_MoreRowsThanLimit_TrimsToLimitAndReportsHasMoreTrue()
    {
        var (service, repository) = BuildService();
        var now = DateTimeOffset.UtcNow;
        // 11 rows for a limit of 10 — the "fetch one extra" signal.
        repository.RowsToReturn = Enumerable.Range(0, 11)
            .Select(i => BuildOrderDto(now.AddMinutes(-i), Guid.NewGuid()))
            .ToList();

        var page = await service.ListAsync(CreatorSlug, OwnerUserId, null, null, null, null, 10, CancellationToken.None);

        Assert.Equal(10, page.Orders.Count);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task ListAsync_FewerRowsThanLimit_ReportsHasMoreFalse()
    {
        var (service, repository) = BuildService();
        var now = DateTimeOffset.UtcNow;
        repository.RowsToReturn = Enumerable.Range(0, 3)
            .Select(i => BuildOrderDto(now.AddMinutes(-i), Guid.NewGuid()))
            .ToList();

        var page = await service.ListAsync(CreatorSlug, OwnerUserId, null, null, null, null, 10, CancellationToken.None);

        Assert.Equal(3, page.Orders.Count);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task ListAsync_ZeroLimit_ClampsToDefaultLimitOfTenPlusOneOnRepository()
    {
        var (service, repository) = BuildService();

        await service.ListAsync(CreatorSlug, OwnerUserId, null, null, null, null, 0, CancellationToken.None);

        Assert.Equal(11, repository.LastLimit); // DefaultLimit(10) + 1 lookahead row
    }

    [Fact]
    public async Task ListAsync_LimitAboveMax_ClampsToMaxLimitOfHundredPlusOneOnRepository()
    {
        var (service, repository) = BuildService();

        await service.ListAsync(CreatorSlug, OwnerUserId, null, null, null, null, 5000, CancellationToken.None);

        Assert.Equal(101, repository.LastLimit); // MaxLimit(100) + 1 lookahead row
    }

    [Fact]
    public async Task ListAsync_PassesCursorThroughToRepositoryUnchanged()
    {
        var (service, repository) = BuildService();
        var cursorCreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var cursorId = Guid.NewGuid();

        await service.ListAsync(CreatorSlug, OwnerUserId, null, null, cursorCreatedAt, cursorId, 10, CancellationToken.None);

        Assert.Equal(CreatorSlug, repository.LastCreatorSlug);
        Assert.Equal(OwnerUserId, repository.LastOwnerUserId);
        Assert.Equal(cursorCreatedAt, repository.LastAfterCreatedAt);
        Assert.Equal(cursorId, repository.LastAfterId);
    }
}
