using CreatorPlatform.Api.Hosting;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>One throwaway PostgreSQL 16 container for the whole test run, migrated with the real EF migrations.
/// Tests isolate their data by seeding their own creator (see <see cref="TestData"/>) rather than sharing rows,
/// so they never depend on each other or on execution order.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("creator_platform_tests")
        .WithUsername("creator_platform")
        .WithPassword("creator_platform_tests")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>For tests that host the API's own DbContext registration (e.g. the health endpoint).</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>A fresh context per unit of work, as the API's scoped lifetime would give — never share one
    /// across the "act" and "assert" steps, or the identity map hides what is actually in the database.</summary>
    public CreatorPlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CreatorPlatformDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseEntityConfigurationsFrom(ApiEntityConfigurations.Assemblies)
            .ConfigureWarnings(warnings => warnings.Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning))
            .Options;

        return new CreatorPlatformDbContext(options);
    }
}

/// <summary>The database tests. Also gets a Redis, for the tests that need both (the health endpoint, the services
/// behind a Redis cache); tests that need only Redis use <see cref="RedisCollection"/>.</summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>, ICollectionFixture<RedisFixture>
{
    public const string Name = "Postgres";
}
