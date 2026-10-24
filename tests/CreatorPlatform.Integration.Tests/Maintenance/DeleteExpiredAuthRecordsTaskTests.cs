using CreatorPlatform.Auth.Infrastructure.Maintenance;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Maintenance;

/// <summary>U2: sessions and one-time tokens go 30 days after they expired (sessions also 30 days after revocation);
/// anything younger stays.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DeleteExpiredAuthRecordsTaskTests
{
    private readonly PostgresFixture _postgres;
    private readonly TestData _data;

    public DeleteExpiredAuthRecordsTaskTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _data = new TestData(postgres);
    }

    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private async Task<Guid> AddSessionAsync(int userId, DateTimeOffset expiresAt, DateTimeOffset? revokedAt = null)
    {
        await using var db = _postgres.CreateDbContext();
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO auth.user_sessions ("Id", "UserId", "SessionTokenHash", "ExpiresAt", "RevokedAt", "CreatedAt")
            VALUES ({id}, {userId}, {Guid.NewGuid().ToString("N")}, {expiresAt}, {revokedAt}, {expiresAt.AddDays(-7)})
            """);
        return id;
    }

    private async Task<Guid> AddTokenAsync(string table, int userId, DateTimeOffset expiresAt)
    {
        await using var db = _postgres.CreateDbContext();
        var id = Guid.NewGuid();
        var hash = Guid.NewGuid().ToString("N");
        var createdAt = expiresAt.AddHours(-24);
        if (table == "email_change_tokens")
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO auth.email_change_tokens ("Id", "UserId", "NewEmail", "TokenHash", "ExpiresAt", "CreatedAt")
                VALUES ({id}, {userId}, {TestData.UniqueEmail("new")}, {hash}, {expiresAt}, {createdAt})
                """);
        else if (table == "password_reset_tokens")
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO auth.password_reset_tokens ("Id", "UserId", "TokenHash", "ExpiresAt", "CreatedAt")
                VALUES ({id}, {userId}, {hash}, {expiresAt}, {createdAt})
                """);
        else
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO auth.email_verification_tokens ("Id", "UserId", "TokenHash", "ExpiresAt", "CreatedAt")
                VALUES ({id}, {userId}, {hash}, {expiresAt}, {createdAt})
                """);
        return id;
    }

    private async Task<bool> ExistsAsync(string table, Guid id)
    {
        await using var db = _postgres.CreateDbContext();
        var count = await db.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*)::int AS \"Value\" FROM auth.{table} WHERE \"Id\" = {{0}}", id)
            .SingleAsync();
        return count == 1;
    }

    private DeleteExpiredAuthRecordsTask Task(int batchSize = 1000) =>
        new(_postgres.CreateDbContext(), Options.Create(new MaintenanceOptions { BatchSize = batchSize }));

    [Fact]
    public async Task SessionsExpiredOrRevokedOverThirtyDaysAgo_AreDeleted_YoungerOnesStay()
    {
        var userId = await _data.CreateUserAsync();
        var expiredLongAgo = await AddSessionAsync(userId, Now.AddDays(-31));
        var revokedLongAgo = await AddSessionAsync(userId, Now.AddDays(5), revokedAt: Now.AddDays(-31));
        var expiredRecently = await AddSessionAsync(userId, Now.AddDays(-29));
        var revokedRecently = await AddSessionAsync(userId, Now.AddDays(5), revokedAt: Now.AddDays(-1));
        var active = await AddSessionAsync(userId, Now.AddDays(7));

        var deleted = await Task(batchSize: 1).RunAsync(Now, CancellationToken.None);

        Assert.True(deleted >= 2);
        Assert.False(await ExistsAsync("user_sessions", expiredLongAgo));
        Assert.False(await ExistsAsync("user_sessions", revokedLongAgo));
        Assert.True(await ExistsAsync("user_sessions", expiredRecently));
        Assert.True(await ExistsAsync("user_sessions", revokedRecently));
        Assert.True(await ExistsAsync("user_sessions", active));
    }

    [Theory]
    [InlineData("email_verification_tokens")]
    [InlineData("password_reset_tokens")]
    [InlineData("email_change_tokens")]
    public async Task TokensExpiredOverThirtyDaysAgo_AreDeleted_YoungerOnesStay(string table)
    {
        var userId = await _data.CreateUserAsync();
        var old = await AddTokenAsync(table, userId, Now.AddDays(-31));
        var recent = await AddTokenAsync(table, userId, Now.AddDays(-29));
        var live = await AddTokenAsync(table, userId, Now.AddHours(1));

        await Task().RunAsync(Now, CancellationToken.None);

        Assert.False(await ExistsAsync(table, old));
        Assert.True(await ExistsAsync(table, recent));
        Assert.True(await ExistsAsync(table, live));
        Assert.Equal(0, await Task().RunAsync(Now, CancellationToken.None));
    }
}
