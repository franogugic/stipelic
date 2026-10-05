using CreatorPlatform.Creators.Application;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using CreatorPlatform.Creators.Infrastructure.Services;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Infrastructure.Repositories;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreatorPlatform.Integration.Tests.Creators;

/// <summary>Deleting a workspace settles its money first: open bank-transfer balances block it, paid Stripe billing
/// is cancelled immediately, a pending Checkout is expired, and any Stripe failure leaves everything unchanged.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DeleteWorkspaceTests
{
    private const string SessionId = "cs_test_delete_pending";

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;
    private readonly FakeCheckoutSessionService _checkout = new();
    private readonly FakeSubscriptionCancellationService _cancellation = new();

    // xUnit creates the class per test, so every test owns its Stripe subscription id (the DB is shared).
    private readonly string _stripeSubscriptionId = $"sub_test_{Guid.NewGuid():N}";

    public DeleteWorkspaceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    /// <summary>The production service over the real repositories, on its own context (a request scope).</summary>
    private async Task<int> DeleteAsync(TestData.SeededCreator creator)
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
            _checkout,
            _cancellation,
            new UnexpectedBillingPortalService(),
            new CreatorOpenBalanceCheck(db),
            new UnexpectedBillingCustomerService(),
            NullLogger<CreatorService>.Instance);

        return await service.DeleteCurrentAsync(creator.OwnerUserId, CancellationToken.None);
    }

    // ---- Balances --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1_000)]
    [InlineData(-500)]
    public async Task BankTransferWithABalance_Is409_AndNothingChanges(int balanceCents)
    {
        var creator = await CreateBankTransferCreatorAsync();
        await AddLedgerEntryAsync(creator.CreatorId, balanceCents, "Eur");

        var exception = await Assert.ThrowsAsync<ConflictException>(() => DeleteAsync(creator));

        Assert.Equal(CreatorErrorCodes.WorkspaceHasBalance, exception.Code);
        Assert.Equal("Request a payout of your remaining balance before deleting this workspace.", exception.Message);
        await AssertUntouchedAsync(creator, "Active");
    }

    [Fact]
    public async Task ABalanceInAnyCurrency_Blocks()
    {
        var creator = await CreateBankTransferCreatorAsync();
        await AddLedgerEntryAsync(creator.CreatorId, 1_000, "Eur");
        await AddLedgerEntryAsync(creator.CreatorId, -1_000, "Eur");
        await AddLedgerEntryAsync(creator.CreatorId, 300, "Usd");

        var exception = await Assert.ThrowsAsync<ConflictException>(() => DeleteAsync(creator));

        Assert.Equal(CreatorErrorCodes.WorkspaceHasBalance, exception.Code);
        await AssertUntouchedAsync(creator, "Active");
    }

    [Fact]
    public async Task APendingPayout_Is409_EvenWithTheLedgerAtZero()
    {
        // Requesting a payout debits the ledger, so the balance reads 0 while the money is still owed.
        var creator = await CreateBankTransferCreatorAsync();
        await AddLedgerEntryAsync(creator.CreatorId, 2_000, "Eur");
        await AddLedgerEntryAsync(creator.CreatorId, -2_000, "Eur");
        await AddPayoutAsync(creator.CreatorId, 2_000, "Pending");

        var exception = await Assert.ThrowsAsync<ConflictException>(() => DeleteAsync(creator));

        Assert.Equal(CreatorErrorCodes.WorkspaceHasBalance, exception.Code);
        await AssertUntouchedAsync(creator, "Active");
    }

    [Fact]
    public async Task ASettledBankTransferWorkspace_IsDeleted()
    {
        var creator = await CreateBankTransferCreatorAsync();
        await AddLedgerEntryAsync(creator.CreatorId, 2_000, "Eur");
        await AddLedgerEntryAsync(creator.CreatorId, -2_000, "Eur");
        await AddPayoutAsync(creator.CreatorId, 2_000, "Paid");

        Assert.Equal(creator.CreatorId, await DeleteAsync(creator));

        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
        Assert.Equal("Cancelled", Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId)).Status);
    }

    [Fact]
    public async Task AStripeConnectWorkspace_IsNotCheckedForALedgerBalance()
    {
        // Connect sales settle in Stripe; a stray ledger row must not block a Connect workspace.
        var creator = await _data.CreateCreatorAsync();
        await AddLedgerEntryAsync(creator.CreatorId, 1_000, "Eur");

        await DeleteAsync(creator);

        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
    }

    // ---- Paid Stripe subscription --------------------------------------------------------------------------

    [Theory]
    [InlineData("Active")]
    [InlineData("PastDue")]
    public async Task APaidSubscription_IsCancelledInStripeImmediately_ThenTheWorkspaceIsDisabled(string status)
    {
        var creator = await CreatePaidStripeCreatorAsync(status);

        await DeleteAsync(creator);

        Assert.Equal([_stripeSubscriptionId], _cancellation.CancelImmediatelyCalls);
        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
        var subscription = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.Equal("Cancelled", subscription.Status);
        Assert.NotNull(subscription.CancelledAt);
        Assert.Empty(_checkout.ExpireCalls);
    }

    [Fact]
    public async Task AStripeFailure_ChangesNothing()
    {
        var creator = await CreatePaidStripeCreatorAsync("Active");
        _cancellation.CancelFailure = new HttpRequestException("Stripe is unreachable.");

        await Assert.ThrowsAsync<HttpRequestException>(() => DeleteAsync(creator));

        await AssertUntouchedAsync(creator, "Active");
    }

    [Fact]
    public async Task AFreeWorkspace_IsDeletedWithoutCallingStripe()
    {
        var creator = await _data.CreateCreatorAsync();

        await DeleteAsync(creator);

        Assert.Empty(_cancellation.CancelImmediatelyCalls);
        Assert.Empty(_checkout.ExpireCalls);
        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
        Assert.Equal("Cancelled", Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId)).Status);
    }

    // ---- Pending checkout ----------------------------------------------------------------------------------

    [Fact]
    public async Task APendingCheckout_IsExpired_ThenTheWorkspaceIsDisabled()
    {
        var creator = await _data.CreatePendingCreatorAsync(SessionId);

        await DeleteAsync(creator);

        Assert.Equal([SessionId], _checkout.ExpireCalls);
        Assert.Empty(_cancellation.CancelImmediatelyCalls);
        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
        Assert.Equal("Cancelled", Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId)).Status);
    }

    [Fact]
    public async Task ACompletedCheckout_Is409_AndNothingChanges()
    {
        var creator = await _data.CreatePendingCreatorAsync(SessionId);
        _checkout.ExpireOutcome = CheckoutSessionExpireOutcome.AlreadyCompleted;

        var exception = await Assert.ThrowsAsync<ConflictException>(() => DeleteAsync(creator));

        Assert.Equal("Payment already completed.", exception.Message);
        await AssertUntouchedAsync(creator, "PendingPayment");
    }

    [Fact]
    public async Task AFailureToExpireTheCheckout_ChangesNothing()
    {
        var creator = await _data.CreatePendingCreatorAsync(SessionId);
        _checkout.ExpireFailure = new HttpRequestException("Stripe is unreachable.");

        await Assert.ThrowsAsync<HttpRequestException>(() => DeleteAsync(creator));

        await AssertUntouchedAsync(creator, "PendingPayment");
    }

    // ---- Later webhooks ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("active")]
    [InlineData("past_due")]
    [InlineData("canceled")]
    public async Task ALaterSubscriptionUpdate_LeavesTheDeletedWorkspaceAlone(string stripeStatus)
    {
        var creator = await CreatePaidStripeCreatorAsync("Active");
        await DeleteAsync(creator);

        await WithWebhooksAsync(webhooks => webhooks.HandleSubscriptionUpdatedAsync(SubscriptionEvent(stripeStatus), CancellationToken.None));

        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
        Assert.Equal("Cancelled", Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId)).Status);
    }

    [Fact]
    public async Task ALaterSubscriptionDeleted_IsANoOp()
    {
        var creator = await CreatePaidStripeCreatorAsync("Active");
        await DeleteAsync(creator);
        var before = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));

        await WithWebhooksAsync(webhooks => webhooks.HandleSubscriptionDeletedAsync(SubscriptionEvent("canceled"), CancellationToken.None));

        Assert.Equal("Disabled", await _data.GetCreatorStatusAsync(creator.CreatorId));
        var after = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.Equal(("Cancelled", before.CancelledAt), (after.Status, after.CancelledAt));
    }

    // ---- Helpers -------------------------------------------------------------------------------------------

    private SubscriptionChangedData SubscriptionEvent(string status) => new()
    {
        EventId = $"evt_{Guid.NewGuid():N}",
        StripeSubscriptionId = _stripeSubscriptionId,
        StripeCustomerId = "cus_test_delete",
        Status = status,
        OccurredAt = DateTimeOffset.UtcNow.AddMinutes(1),
    };

    private async Task WithWebhooksAsync(Func<CreatorWebhookService, Task> act)
    {
        await using var db = _fixture.CreateDbContext();
        await act(new CreatorWebhookService(
            new CreatorRepository(db),
            new CreatorSubscriptionRepository(db),
            new CreatorPlanRepository(db),
            new WebhookFailureRepository(db),
            new CreatorsUnitOfWork(db),
            new FakeBillingPeriodService(),
            new RecordingCreatorCacheInvalidator(),
            NullLogger<CreatorWebhookService>.Instance));
    }

    private async Task AssertUntouchedAsync(TestData.SeededCreator creator, string expectedCreatorStatus)
    {
        Assert.Equal(expectedCreatorStatus, await _data.GetCreatorStatusAsync(creator.CreatorId));
        var subscription = Assert.Single(await _data.GetSubscriptionsAsync(creator.CreatorId));
        Assert.NotEqual("Cancelled", subscription.Status);
        Assert.Null(subscription.CancelledAt);
    }

    private async Task<TestData.SeededCreator> CreateBankTransferCreatorAsync()
    {
        var creator = await _data.CreateCreatorAsync();
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            UPDATE creators.creators SET "PayoutMode" = 'BankTransfer', "CountryCode" = 'RS' WHERE "Id" = {creator.CreatorId}
            """);
        return creator;
    }

    /// <summary>A workspace on a paid plan billed by this test's Stripe subscription.</summary>
    private async Task<TestData.SeededCreator> CreatePaidStripeCreatorAsync(string status)
    {
        var creator = await _data.CreateCreatorAsync(TestData.BasicPlanId);
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            UPDATE creators.creator_subscriptions
            SET "Provider" = 'Stripe', "Status" = {status}, "ProviderSubscriptionId" = {_stripeSubscriptionId}
            WHERE "CreatorId" = {creator.CreatorId}
            """);
        return creator;
    }

    private async Task AddLedgerEntryAsync(int creatorId, int amountCents, string currency)
    {
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO payouts.ledger_entries ("PublicId", "CreatorId", "Type", "AmountCents", "Currency", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Adjustment', {amountCents}, {currency}, {DateTimeOffset.UtcNow})
            """);
    }

    private async Task AddPayoutAsync(int creatorId, int amountCents, string status)
    {
        await using var db = _fixture.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? paidAt = status == "Paid" ? now : null;
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO payouts.payouts ("PublicId", "CreatorId", "AmountCents", "Currency", "Status", "CreatedAt", "PaidAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {amountCents}, 'Eur', {status}, {now}, {paidAt}, {now})
            """);
    }
}
