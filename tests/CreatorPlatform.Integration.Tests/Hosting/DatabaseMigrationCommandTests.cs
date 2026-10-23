using CreatorPlatform.Api.Hosting;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CreatorPlatform.Integration.Tests.Hosting;

/// <summary>The production migration step (<c>--migrate</c>): a fresh database gets every migration, a second run is a
/// no-op, and a failure is exit code 1 with a Critical log line, so the stack never starts on a half-known schema.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DatabaseMigrationCommandTests
{
    private readonly PostgresFixture _postgres;

    public DatabaseMigrationCommandTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private static ServiceProvider ServicesFor(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CreatorPlatformDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseEntityConfigurationsFrom(ApiEntityConfigurations.Assemblies));
        return services.BuildServiceProvider();
    }

    private async Task<string> CreateEmptyDatabaseAsync(string name)
    {
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(_postgres.ConnectionString) { Database = name }.ConnectionString;
    }

    private async Task DropDatabaseAsync(string name)
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_postgres.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task AnEmptyDatabase_GetsEveryMigration_AndASecondRunAppliesNothing()
    {
        var name = $"migrate_{Guid.NewGuid():N}";
        var connectionString = await CreateEmptyDatabaseAsync(name);
        try
        {
            await using var services = ServicesFor(connectionString);
            var logger = new ListLogger<DatabaseMigrationCommandTests>();

            var first = await DatabaseMigrationCommand.RunAsync(services, logger, CancellationToken.None);

            Assert.Equal(0, first);
            await using (var scope = services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>().Database;
                Assert.Equal(database.GetMigrations(), await database.GetAppliedMigrationsAsync());
                Assert.Empty(await database.GetPendingMigrationsAsync());
            }
            Assert.Contains(logger.Entries, entry =>
                entry.Level == LogLevel.Information && entry.Message.StartsWith("Database migration: applied ", StringComparison.Ordinal));

            var second = await DatabaseMigrationCommand.RunAsync(services, logger, CancellationToken.None);

            Assert.Equal(0, second);
            Assert.Contains(logger.Entries, entry =>
                entry.Level == LogLevel.Information && entry.Message.Contains("nothing to apply", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        }
        finally
        {
            await DropDatabaseAsync(name);
        }
    }

    [Fact]
    public async Task AnUnreachableDatabase_ExitsWithOne_AndLogsCritical()
    {
        var connectionString = $"Host=127.0.0.1;Port={ClosedPort.Find()};Database=x;Username=x;Password=x;Timeout=2";
        await using var services = ServicesFor(connectionString);
        var logger = new ListLogger<DatabaseMigrationCommandTests>();

        var exitCode = await DatabaseMigrationCommand.RunAsync(services, logger, CancellationToken.None);

        Assert.Equal(1, exitCode);
        var critical = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Critical);
        Assert.StartsWith("Database migration failed.", critical.Message);
    }
}

/// <summary>The switch is taken out before the host sees the arguments.</summary>
public sealed class DatabaseMigrationSwitchTests
{
    [Fact]
    public void TheSwitch_IsRemoved_AndTheOtherArgumentsAreKept()
    {
        Assert.True(DatabaseMigrationCommand.TryTake(["--environment", "Production", "--migrate"], out var hostArgs));
        Assert.Equal(["--environment", "Production"], hostArgs);
    }

    [Theory]
    [InlineData]
    [InlineData("--urls", "http://+:8080")]
    [InlineData("--Migrate")]   // exact spelling only
    [InlineData("migrate")]
    public void WithoutTheSwitch_TheHostStartsAsUsual(params string[] args)
    {
        Assert.False(DatabaseMigrationCommand.TryTake(args, out var hostArgs));
        Assert.Equal(args, hostArgs);
    }
}
