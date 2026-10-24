using CreatorPlatform.Api.Hosting;
using CreatorPlatform.Creators.Infrastructure.Maintenance;
using CreatorPlatform.Creators.Infrastructure.Workspaces;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Shared.Infrastructure.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Maintenance;

/// <summary>DEL-1 (Worker part): disabled workspaces not fully erased yet are finished — B12.1's soft deletes once, and
/// tombstones whose files failed — and a tombstone's connected account is deleted after its retention, once its balance
/// is zero. Runs on its own database: the task works on every disabled workspace there is.</summary>
[Collection(PostgresCollection.Name)]
public sealed class FinishDeletedWorkspacesTaskTests : IAsyncLifetime
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private readonly PostgresFixture _postgres;
    private IsolatedDatabase _database = null!;
    private readonly FakeCheckoutSessionService _sessions = new();
    private readonly FakeWorkspaceFiles _files = new();
    private readonly FakeConnectAccounts _connect = new();

    public FinishDeletedWorkspacesTaskTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public async Task InitializeAsync() => _database = await IsolatedDatabase.CreateAsync(_postgres);

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private async Task<int> RunAsync(string stripeKey = "sk_test_x")
    {
        await using var db = _database.CreateDbContext();
        var services = new ServiceCollection()
            .AddScoped(_ => db)
            .AddWorkspaceDataPurgers(ApiEntityConfigurations.Assemblies)
            .AddSingleton<IConnectAccountService>(_connect);
        await using var provider = services.BuildServiceProvider();
        var task = new FinishDeletedWorkspacesTask(
            db,
            new WorkspaceErasure(db, provider.GetServices<IWorkspaceDataPurger>(), _sessions, _files,
                NullLogger<WorkspaceErasure>.Instance),
            provider,
            Options.Create(new MaintenanceOptions { BatchSize = 10 }),
            Options.Create(new StripeOptions { SecretKey = stripeKey }),
            NullLogger<FinishDeletedWorkspacesTask>.Instance);
        return await task.RunAsync(Now, None);
    }

    [Fact]
    public async Task AWorkspaceOnlyDisabledSoFar_IsErasedOnce_AndAnActiveOneIsLeftAlone()
    {
        var legacy = await CreateCreatorAsync("Disabled");
        var active = await CreateCreatorAsync("Active");
        await CreateSoldProductAsync(legacy);
        await CreateSoldProductAsync(active);

        Assert.Equal(1, await RunAsync());
        Assert.Equal(0, await RunAsync());

        var erased = await StateAsync(legacy);
        Assert.Equal(("Deleted workspace", true, true), (erased.Name, erased.Deleted, erased.FilesDeleted));
        Assert.Equal(("Active", false), ((await StateAsync(active)).Status, (await StateAsync(active)).Deleted));
        Assert.Equal([legacy], _files.Calls);
        Assert.Equal(1, await CountAsync($"""SELECT COUNT(*)::int AS "Value" FROM orders.orders WHERE "CreatorId" = {active} AND "Name" IS NOT NULL"""));
    }

    [Fact]
    public async Task FilesThatKeepFailing_AreTriedOncePerRun_AndDeletedWhenStorageIsBack()
    {
        var creator = await CreateCreatorAsync("Disabled");
        _files.Failure = new InvalidOperationException("Blob storage is down.");

        Assert.Equal(0, await RunAsync());
        Assert.Equal([creator], _files.Calls);
        Assert.False((await StateAsync(creator)).FilesDeleted);

        _files.Failure = null;
        Assert.Equal(1, await RunAsync());
        Assert.True((await StateAsync(creator)).FilesDeleted);
    }

    [Fact]
    public async Task AConnectAccount_IsDeletedAfterItsRetention_OnlyAtAZeroBalance()
    {
        var old = await CreateTombstoneAsync("acct_old", deletedDaysAgo: 181);
        var gone = await CreateTombstoneAsync("acct_gone", deletedDaysAgo: 400);
        var owing = await CreateTombstoneAsync("acct_balance", deletedDaysAgo: 200);
        var failing = await CreateTombstoneAsync("acct_failing", deletedDaysAgo: 200);
        var recent = await CreateTombstoneAsync("acct_recent", deletedDaysAgo: 179);
        _connect.Outcomes["acct_gone"] = ConnectAccountDeletionOutcome.AlreadyGone;
        _connect.Outcomes["acct_balance"] = ConnectAccountDeletionOutcome.BalanceNotZero;
        _connect.Failing.Add("acct_failing");

        Assert.Equal(2, await RunAsync());

        Assert.Equal(["acct_balance", "acct_failing", "acct_gone", "acct_old"], _connect.Calls.Order());
        Assert.True((await StateAsync(old)).ConnectDeleted);
        Assert.True((await StateAsync(gone)).ConnectDeleted);
        Assert.False((await StateAsync(owing)).ConnectDeleted);
        Assert.False((await StateAsync(failing)).ConnectDeleted);
        Assert.False((await StateAsync(recent)).ConnectDeleted);
        Assert.Equal("acct_old", (await StateAsync(old)).StripeConnectAccountId);   // kept for the record

        _connect.Calls.Clear();
        await RunAsync();
        Assert.Equal(["acct_balance", "acct_failing"], _connect.Calls.Order());
    }

    [Fact]
    public async Task WithoutAStripeKey_ConnectAccountsAreLeftForLater()
    {
        var tombstone = await CreateTombstoneAsync("acct_nokey", deletedDaysAgo: 365);

        Assert.Equal(0, await RunAsync(stripeKey: ""));

        Assert.Empty(_connect.Calls);
        Assert.False((await StateAsync(tombstone)).ConnectDeleted);
    }

    [Theory]
    [InlineData(119, false)]
    [InlineData(120, true)]
    [InlineData(180, true)]
    [InlineData(3651, false)]
    public void TheConnectAccountRetention_CannotEndInsideTheDisputeWindow(int days, bool valid)
    {
        var result = new MaintenanceOptionsValidator().Validate(null, new MaintenanceOptions { DeletedConnectAccountRetentionDays = days });

        Assert.Equal(valid, result.Succeeded);
    }

    [Fact]
    public async Task EveryPurgerOfTheModules_IsRegistered_EachInItsOwnPlace()
    {
        var registered = WorkspacePurgerServiceCollectionExtensions.FindPurgers(ApiEntityConfigurations.Assemblies);
        var everyPurger = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("CreatorPlatform.", StringComparison.Ordinal) == true)
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IWorkspaceDataPurger).IsAssignableFrom(type)
                           && type.Namespace?.StartsWith("CreatorPlatform.Integration", StringComparison.Ordinal) != true)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(everyPurger, registered);
        Assert.Equal(9, registered.Count);

        await using var db = _database.CreateDbContext();
        var orders = registered.Select(type => ((IWorkspaceDataPurger)Activator.CreateInstance(type, db)!).Order).ToList();
        Assert.Equal(orders.Count, orders.Distinct().Count());
    }

    // ---- Seeding and reading -------------------------------------------------------------------------------------

    private sealed record CreatorState(
        string Status, string Name, bool Deleted, bool FilesDeleted, bool ConnectDeleted, string? StripeConnectAccountId);

    private async Task<int> CreateCreatorAsync(string status, string? connectAccountId = null)
    {
        await using var db = _database.CreateDbContext();
        var user = await ScalarAsync(db, $"""
            INSERT INTO auth.users ("PublicId", "Email", "PasswordHash", "FirstName", "LastName", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {TestData.UniqueEmail("owner")}, 'x', 'Test', 'Owner', 'Active', {Now}, {Now})
            RETURNING "Id" AS "Value"
            """);
        return await ScalarAsync(db, $"""
            INSERT INTO creators.creators ("PublicId", "OwnerUserId", "Name", "Slug", "Status", "DefaultCurrency", "CountryCode",
                                           "StripeConnectAccountId", "StripeConnectDetailsSubmitted",
                                           "StripeConnectChargesEnabled", "StripeConnectPayoutsEnabled", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {user}, 'Studio', {$"s-{Guid.NewGuid():N}"[..30]}, {status}, 'Eur', 'HR',
                    {connectAccountId}, true, true, true, {Now}, {Now})
            RETURNING "Id" AS "Value"
            """);
    }

    /// <summary>A workspace erased <paramref name="deletedDaysAgo"/> days ago, files gone, Connect account still there.</summary>
    private async Task<int> CreateTombstoneAsync(string connectAccountId, int deletedDaysAgo)
    {
        var creator = await CreateCreatorAsync("Disabled", connectAccountId);
        await using var db = _database.CreateDbContext();
        var deletedAt = Now.AddDays(-deletedDaysAgo);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE creators.creators SET "OwnerUserId" = NULL, "DeletedAt" = {deletedAt}, "FilesDeletedAt" = {deletedAt}
            WHERE "Id" = {creator}
            """);
        return creator;
    }

    private async Task CreateSoldProductAsync(int creatorId)
    {
        await using var db = _database.CreateDbContext();
        var product = await ScalarAsync(db, $"""
            INSERT INTO products.products ("PublicId", "CreatorId", "Name", "PriceCents", "Status", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, 'Course', 2900, 'Draft', {Now}, {Now})
            RETURNING "Id" AS "Value"
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO orders.orders ("PublicId", "CreatorId", "ProductId", "Email", "Name", "AmountCents", "Currency", "Status",
                                       "StripeCheckoutSessionId", "PaidAt", "CreatedAt", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {creatorId}, {product}, 'buyer@example.test', 'Buyer', 2900, 'Eur', 'Paid',
                    {$"cs_{Guid.NewGuid():N}"}, {Now}, {Now}, {Now})
            """);
    }

    private async Task<CreatorState> StateAsync(int creatorId)
    {
        await using var db = _database.CreateDbContext();
        return (await db.Database.SqlQuery<CreatorState>($"""
            SELECT "Status", "Name", "DeletedAt" IS NOT NULL AS "Deleted", "FilesDeletedAt" IS NOT NULL AS "FilesDeleted",
                   "ConnectAccountDeletedAt" IS NOT NULL AS "ConnectDeleted", "StripeConnectAccountId"
            FROM creators.creators WHERE "Id" = {creatorId}
            """).ToListAsync()).Single();
    }

    private async Task<int> CountAsync(FormattableString sql)
    {
        await using var db = _database.CreateDbContext();
        return (await db.Database.SqlQuery<int>(sql).ToListAsync()).Single();
    }

    private static async Task<int> ScalarAsync(CreatorPlatformDbContext db, FormattableString sql) =>
        (await db.Database.SqlQuery<int>(sql).ToListAsync()).Single();

    private sealed class FakeWorkspaceFiles : IWorkspaceFiles
    {
        public Exception? Failure { get; set; }

        public List<int> Calls { get; } = [];

        public Task DeleteAllAsync(int creatorId, CancellationToken ct)
        {
            Calls.Add(creatorId);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class FakeConnectAccounts : IConnectAccountService
    {
        public Dictionary<string, ConnectAccountDeletionOutcome> Outcomes { get; } = [];

        public HashSet<string> Failing { get; } = [];

        public List<string> Calls { get; } = [];

        public Task<ConnectAccountDeletionOutcome> DeleteIfBalanceIsZeroAsync(string accountId, CancellationToken ct)
        {
            Calls.Add(accountId);
            if (Failing.Contains(accountId))
                throw new HttpRequestException("Stripe is unreachable.");
            return Task.FromResult(Outcomes.GetValueOrDefault(accountId, ConnectAccountDeletionOutcome.Deleted));
        }

        public Task<string> CreateAccountAsync(string countryCode, string email, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> CreateOnboardingLinkAsync(string accountId, string returnUrl, string refreshUrl, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<PayoutScheduleDto?> GetPayoutScheduleAsync(string accountId, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> CreateDashboardLoginLinkAsync(string accountId, CancellationToken ct) => throw new NotSupportedException();
    }
}
