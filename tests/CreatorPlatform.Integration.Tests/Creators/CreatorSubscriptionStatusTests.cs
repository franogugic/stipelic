using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Creators;

/// <summary>The current workspace carries its current subscription's status, so the shell and Settings can show a
/// past-due plan while the workspace itself stays Active.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CreatorSubscriptionStatusTests
{
    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public CreatorSubscriptionStatusTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    private async Task<CreatorResponseDto?> GetCurrentAsync(int ownerUserId)
    {
        await using var db = _fixture.CreateDbContext();
        var service = new CreatorService(
            new CreatorRepository(db),
            new CreatorMemberRepository(db),
            new CreatorPlanRepository(db),
            new CreatorSettingsRepository(db),
            new CreatorSubscriptionRepository(db),
            new CreatorPayoutProfileRepository(db),
            new CreatorsUnitOfWork(db),
            new FakeCheckoutSessionService(),
            new UnexpectedSubscriptionCancellationService(),
            new UnexpectedBillingPortalService(),
            new CreatorOpenBalanceCheck(db),
            new UnexpectedBillingCustomerService(),
            NullLogger<CreatorService>.Instance);
        return await service.GetCurrentForOwnerAsync(ownerUserId, CancellationToken.None);
    }

    [Fact]
    public async Task APastDueSubscription_IsReported_WhileTheWorkspaceStaysActive()
    {
        var creator = await _data.CreateCreatorAsync(TestData.BasicPlanId);
        await SetSubscriptionStatusAsync(creator.CreatorId, "PastDue");

        var response = await GetCurrentAsync(creator.OwnerUserId);

        Assert.NotNull(response);
        Assert.Equal(("Active", "PastDue", "basic"), (response.Status, response.SubscriptionStatus, response.PlanCode));
    }

    [Fact]
    public async Task ActiveAndPendingSubscriptions_AreReported()
    {
        var active = await _data.CreateCreatorAsync();
        var pending = await _data.CreatePendingCreatorAsync("cs_test_status");

        Assert.Equal("Active", (await GetCurrentAsync(active.OwnerUserId))?.SubscriptionStatus);
        Assert.Equal("PendingPayment", (await GetCurrentAsync(pending.OwnerUserId))?.SubscriptionStatus);
    }

    [Fact]
    public async Task WithoutACurrentSubscription_TheStatusIsNull()
    {
        // Only a Cancelled subscription is left: it is never the current one.
        var creator = await _data.CreateCreatorAsync(TestData.BasicPlanId);
        await SetSubscriptionStatusAsync(creator.CreatorId, "Cancelled");

        var response = await GetCurrentAsync(creator.OwnerUserId);

        Assert.NotNull(response);
        Assert.Null(response.SubscriptionStatus);
        Assert.Equal(string.Empty, response.PlanCode);
    }

    private async Task SetSubscriptionStatusAsync(int creatorId, string status)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            UPDATE creators.creator_subscriptions SET "Status" = {status} WHERE "CreatorId" = {creatorId}
            """);
    }
}
