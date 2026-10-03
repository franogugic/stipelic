using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.MoneyPath.Tests.Fakes;
using CreatorPlatform.Shared.Application.Exceptions;

namespace CreatorPlatform.MoneyPath.Tests.Orders;

public class OrdersExportTests
{
    private const string Slug = "acme";
    private const int OwnerUserId = 1;
    private static readonly Guid OrderId = Guid.Parse("6f9a4c1e-0b7d-4e55-9a3f-1c2d3e4f5a6b");

    private static OrderDto Order(
        string? name = "Ana Kovač", string product = "Preset Pack", string? landingPage = "Spring Sale",
        int amountCents = 2900, int feeCents = 145, string status = "Paid", Guid? id = null, DateTimeOffset? createdAt = null) => new(
        id ?? OrderId,
        "ana@example.com",
        name,
        product,
        amountCents,
        "Eur",
        status,
        createdAt ?? new DateTimeOffset(2026, 9, 30, 14, 5, 9, TimeSpan.FromHours(2)),
        null,
        feeCents,
        amountCents - feeCents,
        landingPage);

    [Fact]
    public void Header_AndFileName_MatchTheContract()
    {
        Assert.Equal(
            "order_id,created_at,customer_name,customer_email,product,landing_page,status,amount,platform_fee,net,currency",
            OrdersCsv.Header);
        Assert.Equal("orders-acme-2026-10-02.csv", OrdersCsv.FileName("acme", new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Row_FormatsMoneyColumnsAsDecimalsAndDateAsIsoUtc()
    {
        Assert.Equal(
            $"{OrderId},2026-09-30T12:05:09Z,Ana Kovač,ana@example.com,Preset Pack,Spring Sale,Paid,29.00,1.45,27.55,EUR",
            OrdersCsv.Row(Order()));
    }

    [Theory]
    [InlineData(1999, 99, "19.99,0.99,19.00")]
    [InlineData(5, 0, "0.05,0.00,0.05")]
    [InlineData(150000, 7500, "1500.00,75.00,1425.00")]
    public void Row_AmountFeeAndNet_AreExactTwoDecimalStrings(int amountCents, int feeCents, string expectedMoney)
    {
        var row = OrdersCsv.Row(Order(amountCents: amountCents, feeCents: feeCents));

        Assert.Contains($",Paid,{expectedMoney},EUR", row);
    }

    [Fact]
    public void Row_MissingNameAndLandingPage_AreEmptyFields()
    {
        Assert.Equal(
            $"{OrderId},2026-09-30T12:05:09Z,,ana@example.com,Preset Pack,,Paid,29.00,1.45,27.55,EUR",
            OrdersCsv.Row(Order(name: null, landingPage: null)));
    }

    [Fact]
    public void Row_FormulaLikeCustomerAndProductValues_AreGuarded()
    {
        var row = OrdersCsv.Row(Order(name: "=HYPERLINK(\"http://evil\",\"x\")", product: "+Bonus", landingPage: "-50% off"));

        Assert.Equal(
            $"{OrderId},2026-09-30T12:05:09Z,\"'=HYPERLINK(\"\"http://evil\"\",\"\"x\"\")\",ana@example.com,'+Bonus,'-50% off,Paid,29.00,1.45,27.55,EUR",
            row);
    }

    [Fact]
    public async Task StartExportAsync_StreamsEveryOrderAcrossKeysetBatchesNewestFirst()
    {
        var repository = new FakeOrderListingRepository();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        // 1,101 orders; pairs share a timestamp so the PublicId tie-breaker is exercised across batch borders.
        repository.AllRows = Enumerable.Range(0, 1101)
            .Select(i => Order(id: Guid.NewGuid(), createdAt: start.AddMinutes(i / 2)))
            .ToList();
        var service = new OrderService(repository, new FakeOrderListingHomeSummaryCache(), new FakeDashboardTrendsCache());

        var export = await service.StartExportAsync(Slug, OwnerUserId, null, null, "paid", "  ana ", CancellationToken.None);
        var exported = new List<OrderDto>();
        await foreach (var order in export.Orders)
            exported.Add(order);

        Assert.Equal(1101, exported.Count);
        Assert.Equal(1101, exported.Select(o => o.PublicId).Distinct().Count());
        Assert.Equal(exported.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.PublicId), exported);
        Assert.Equal(3, repository.CallCount); // 500 + 500 + 101
        Assert.Equal("ana", repository.LastCustomerSearch);
        Assert.Equal(CreatorPlatform.Orders.Domain.Orders.OrderStatus.Paid, repository.LastStatus);
    }

    [Fact]
    public async Task StartExportAsync_UnknownWorkspace_ThrowsNotFoundBeforeStreaming()
    {
        var repository = new FakeOrderListingRepository { CreatorExists = false };
        var service = new OrderService(repository, new FakeOrderListingHomeSummaryCache(), new FakeDashboardTrendsCache());

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.StartExportAsync(Slug, OwnerUserId, null, null, null, null, CancellationToken.None));

        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task StartExportAsync_InvalidStatus_ThrowsBadRequestBeforeStreaming()
    {
        var repository = new FakeOrderListingRepository();
        var service = new OrderService(repository, new FakeOrderListingHomeSummaryCache(), new FakeDashboardTrendsCache());

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.StartExportAsync(Slug, OwnerUserId, null, null, "shipped", null, CancellationToken.None));

        Assert.Equal(0, repository.CallCount);
    }
}
