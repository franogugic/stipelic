using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Shared.Application.Exceptions;
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

    [Theory]
    [InlineData("90d")]
    [InlineData("12m")]
    [InlineData("nope")]
    public async Task APeriodSummary_RejectsAPeriodOutsideTheMenu(string period)
    {
        var (service, _) = BuildService();

        var error = await Assert.ThrowsAsync<BadRequestException>(
            () => service.GetPeriodSummaryAsync("acme", 1, period, CancellationToken.None));

        Assert.Contains("today, 7d, 15d, 30d, 2m, 3m, 6m, all", error.Message);
    }

    [Fact]
    public async Task APeriodSummary_OfAWorkspaceThatIsNotTheCallers_Is404()
    {
        var (service, repository) = BuildService();
        repository.CreatorExists = false;

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetPeriodSummaryAsync("acme", 1, "30d", CancellationToken.None));
    }

    private static (OrderService Service, FakeOrderListingRepository Repository) BuildService()
    {
        var repository = new FakeOrderListingRepository();
        var service = new OrderService(repository, new FakeOrderListingHomeSummaryCache(), new FakeDashboardTrendsCache());
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

        var page = await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(), null, null, null, 10, CancellationToken.None);

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

        var page = await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(), null, null, null, 10, CancellationToken.None);

        Assert.Equal(3, page.Orders.Count);
        Assert.False(page.HasMore);
    }

    [Fact]
    public async Task ListAsync_ZeroLimit_ClampsToDefaultLimitOfTenPlusOneOnRepository()
    {
        var (service, repository) = BuildService();

        await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(), null, null, null, 0, CancellationToken.None);

        Assert.Equal(11, repository.LastLimit); // DefaultLimit(10) + 1 lookahead row
    }

    [Fact]
    public async Task ListAsync_LimitAboveMax_ClampsToMaxLimitOfHundredPlusOneOnRepository()
    {
        var (service, repository) = BuildService();

        await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(), null, null, null, 5000, CancellationToken.None);

        Assert.Equal(101, repository.LastLimit); // MaxLimit(100) + 1 lookahead row
    }

    [Fact]
    public async Task ListAsync_PassesCursorThroughToRepositoryUnchanged()
    {
        var (service, repository) = BuildService();
        var cursorCreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var cursorId = Guid.NewGuid();

        await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(), cursorCreatedAt, null, cursorId, 10, CancellationToken.None);

        Assert.Equal(CreatorSlug, repository.LastCreatorSlug);
        Assert.Equal(OwnerUserId, repository.LastOwnerUserId);
        Assert.Equal(cursorCreatedAt, repository.LastAfterCreatedAt);
        Assert.Equal(cursorId, repository.LastAfterId);
    }

    // ---- R6.5: the period and product filters --------------------------------------------------------------------

    private static OrderDto Counted(string status, DateTimeOffset countedAt, Guid? id = null) =>
        BuildOrderDto(countedAt.AddDays(-40), id ?? Guid.NewGuid()) with { Status = status, CountedAt = countedAt };

    [Fact]
    public async Task WithAPeriod_TheListHasOnlyTheOrdersCountedInIt_WithTheTotalAndTheTabCounts()
    {
        var (service, repository) = BuildService();
        var now = DateTimeOffset.UtcNow;
        repository.AllRows =
        [
            Counted("Paid", now.AddDays(-1)),
            Counted("Paid", now.AddDays(-2)),
            Counted("Refunded", now.AddDays(-3)),    // placed 43 days ago, refunded in the period: it counts here
            Counted("Pending", now.AddDays(-4)),
            Counted("Failed", now.AddDays(-5)),
            Counted("Paid", now.AddDays(-20)),       // outside the 7 days
        ];

        var all = await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(Period: "7d"), null, null, null, 10, CancellationToken.None);
        var paid = await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(Status: "paid", Period: "7d"), null, null, null, 10, CancellationToken.None);

        Assert.Equal(5, all.Orders.Count);
        Assert.Equal(all.Orders.OrderByDescending(o => o.CountedAt), all.Orders);    // newest counted first
        Assert.Equal(5, all.Total);
        Assert.Equal(new OrderStatusCountsDto(5, 2, 1, 1, 1), all.StatusCounts);
        Assert.Equal(2, paid.Total);
        Assert.Equal(new OrderStatusCountsDto(5, 2, 1, 1, 1), paid.StatusCounts);  // the tabs ignore the status filter
        Assert.NotNull(repository.LastFilter!.Period);
        Assert.Equal(now, repository.LastFilter.Period!.To, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task WithoutAPeriod_TheListIsAsBefore_NoTotals_AndTheCursorIsCreatedAt()
    {
        var (service, repository) = BuildService();
        var createdCursor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var countedCursor = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var id = Guid.NewGuid();

        var plain = await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(), createdCursor, countedCursor, id, 10, CancellationToken.None);
        Assert.Equal((null, null), (plain.Total, plain.StatusCounts));
        Assert.Null(repository.LastFilter!.Period);
        Assert.Equal(createdCursor, repository.LastAfterCreatedAt);

        await service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(Period: "30d"), createdCursor, countedCursor, id, 10, CancellationToken.None);
        Assert.Equal(countedCursor, repository.LastAfterCreatedAt);   // with a period the counted-date cursor is used
        Assert.Equal(id, repository.LastAfterId);
    }

    [Fact]
    public async Task AnotherCreatorsProduct_IsNotFound_AndABadPeriodIsRejected_BeforeAnyRead()
    {
        var (service, repository) = BuildService();
        var foreign = Guid.NewGuid();
        repository.ForeignProducts.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(ProductId: foreign, Period: "7d"), null, null, null, 10, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.StartExportAsync(CreatorSlug, OwnerUserId, new OrderListQuery(ProductId: foreign), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetPeriodSummaryAsync(CreatorSlug, OwnerUserId, "7d", CancellationToken.None, foreign));
        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.ListAsync(CreatorSlug, OwnerUserId, new OrderListQuery(Period: "90d"), null, null, null, 10, CancellationToken.None));
        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task TheExport_WithAPeriod_StreamsWhatTheListShows_ByCountedDate_AcrossBatches()
    {
        var (service, repository) = BuildService();
        var now = DateTimeOffset.UtcNow;
        repository.AllRows = Enumerable.Range(0, 1203)
            .Select(i => Counted(i % 4 == 0 ? "Refunded" : "Paid", now.AddMinutes(-(i / 2) - 1)))   // pairs share a counted time
            .Append(Counted("Paid", now.AddDays(-40)))                                             // outside 30 days
            .ToList();

        var export = await service.StartExportAsync(CreatorSlug, OwnerUserId, new OrderListQuery(Period: "30d"), CancellationToken.None);
        var rows = new List<OrderDto>();
        await foreach (var order in export.Orders)
            rows.Add(order);

        Assert.Equal(1203, rows.Count);
        Assert.Equal(1203, rows.Select(o => o.PublicId).Distinct().Count());
        Assert.Equal(rows.OrderByDescending(o => o.CountedAt).ThenByDescending(o => o.PublicId), rows);
    }
}
