using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Receipts;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Orders;

/// <summary>The purchase success page's receipt, read by the Checkout session id from the success URL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class OrderReceiptTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public OrderReceiptTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<OrderReceiptDto> GetAsync(string? sessionId)
    {
        await using var db = _fixture.CreateDbContext();
        return await new OrderReceiptService(new OrderReceiptReader(db)).GetBySessionIdAsync(sessionId, CancellationToken.None);
    }

    [Fact]
    public async Task APaidOrder_ReturnsTheReceiptWithTheCreatorsBrand()
    {
        var creator = await _data.CreateCreatorAsync();
        await AddSettingsAsync(creator.CreatorId, brandName: "MH Studio", color: "#4F74D9", logoUrl: "https://cdn.example.test/logo.png", supportEmail: "hello@mh.test");
        var productId = await AddProductAsync(creator.CreatorId, "Adriatic Summer Presets");
        var paidAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var (sessionId, publicId) = await AddOrderAsync(creator.CreatorId, productId, "ana.kovacevic@gmail.com", "Ana Kovačević", "Paid", paidAt);

        var receipt = await GetAsync(sessionId);

        Assert.Equal(OrderNumbers.From(publicId), receipt.OrderNumber);
        Assert.Equal(8, receipt.OrderNumber.Length);
        Assert.Equal(("Paid", "Ana", "a•••@gmail.com"), (receipt.Status, receipt.BuyerFirstName, receipt.BuyerEmail));
        Assert.Equal(("Adriatic Summer Presets", 2900, "Eur"), (receipt.ProductName, receipt.AmountCents, receipt.Currency));
        Assert.Equal(paidAt.ToUnixTimeMilliseconds(), receipt.PaidAt!.Value.ToUnixTimeMilliseconds());
        Assert.Equal(
            new OrderReceiptCreatorDto("MH Studio", creator.Slug, "#4F74D9", "https://cdn.example.test/logo.png", "hello@mh.test"),
            receipt.Creator);
    }

    [Fact]
    public async Task BeforeTheWebhook_TheReceiptIsPending()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId, "Course");
        var (sessionId, _) = await AddOrderAsync(creator.CreatorId, productId, "buyer@example.test", null, "Pending", null);

        var receipt = await GetAsync(sessionId);

        Assert.Equal(("Pending", (DateTimeOffset?)null, (string?)null), (receipt.Status, receipt.PaidAt, receipt.BuyerFirstName));
        // No brand settings yet: the workspace name, no colour / logo / support email.
        Assert.Equal(new OrderReceiptCreatorDto("Test Creator", creator.Slug, null, null, null), receipt.Creator);
    }

    [Theory]
    [InlineData("cs_test_does_not_exist")]
    [InlineData("")]
    [InlineData(null)]
    public async Task AnUnknownOrMissingSession_Is404(string? sessionId)
    {
        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(sessionId));
    }

    [Fact]
    public async Task ASessionOlderThan24Hours_Is404()
    {
        var creator = await _data.CreateCreatorAsync();
        var productId = await AddProductAsync(creator.CreatorId, "Old");
        var (sessionId, _) = await AddOrderAsync(
            creator.CreatorId, productId, "buyer@example.test", null, "Paid", DateTimeOffset.UtcNow.AddHours(-25),
            createdAt: DateTimeOffset.UtcNow.AddHours(-25));

        await Assert.ThrowsAsync<NotFoundException>(() => GetAsync(sessionId));
    }

    private async Task AddSettingsAsync(int creatorId, string brandName, string color, string? logoUrl, string? supportEmail)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO creators.creator_settings ("CreatorId", "SupportEmail", "BrandName", "LogoUrl", "PrimaryColor", "Timezone", "Language", "CreatedAt", "UpdatedAt")
            VALUES ({creatorId}, {supportEmail}, {brandName}, {logoUrl}, {color}, 'Europe/Zagreb', 'en', {now}, {now})
            """);
    }

    private async Task<int> AddProductAsync(int creatorId, string name)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        return (await db.Database.SqlQuery<int>($"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Type", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {name}, 2900, 'Digital', 'Active', {now}, {now})
            RETURNING "Id" AS "Value"
            """).ToListAsync()).Single();
    }

    private async Task<(string SessionId, Guid PublicId)> AddOrderAsync(
        int creatorId, int productId, string email, string? name, string status, DateTimeOffset? paidAt,
        DateTimeOffset? createdAt = null)
    {
        await using var db = _fixture.CreateDbContext();
        var sessionId = $"cs_test_{Guid.NewGuid():N}";
        var publicId = Guid.NewGuid();
        var created = createdAt ?? DateTimeOffset.UtcNow.AddMinutes(-2);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "Name", "AmountCents", "Currency", "Status",
                                       "PlatformFeeBasisPoints", "PlatformFeeCents", "PayoutMode", "StripeCheckoutSessionId",
                                       "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({publicId}, {creatorId}, {productId}, {email}, {name}, 2900, 'Eur', {status}, 0, 0, 'StripeConnect', {sessionId},
                    {created}, {paidAt}, {created})
            """);
        return (sessionId, publicId);
    }
}
