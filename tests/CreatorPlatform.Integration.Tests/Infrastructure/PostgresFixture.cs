using System.Reflection;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        LoadInfrastructureAssemblies();

        await _container.StartAsync();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>A fresh context per unit of work, as the API's scoped lifetime would give — never share one
    /// across the "act" and "assert" steps, or the identity map hides what is actually in the database.</summary>
    public CreatorPlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CreatorPlatformDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        return new CreatorPlatformDbContext(options);
    }

    // CreatorPlatformDbContext applies entity configurations from the *.Infrastructure assemblies already loaded
    // into the AppDomain. The Api loads them all through its Add*Infrastructure calls; here nothing references
    // most of them in code, so load every one copied to the output directory to get the same full model.
    private static void LoadInfrastructureAssemblies()
    {
        foreach (var path in Directory.GetFiles(AppContext.BaseDirectory, "CreatorPlatform.*.Infrastructure.dll"))
            Assembly.Load(AssemblyName.GetAssemblyName(path));
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
