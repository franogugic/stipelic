using CreatorPlatform.Api.Hosting;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>
/// A fresh, fully migrated database of its own, in the shared Postgres container, for tests that need tables nobody else
/// writes to (e.g. the whole email outbox: other tests leave unsent mail in the shared database). Copied from a migrated
/// template, so creating one takes a fraction of a second. Dropped on dispose.
/// </summary>
public sealed class IsolatedDatabase : IAsyncDisposable
{
    private const string TemplateName = "luma_isolated_template";
    private static readonly SemaphoreSlim TemplateLock = new(1, 1);
    private static string? _templateReadyFor;

    private readonly PostgresFixture _fixture;
    private readonly string _name;

    private IsolatedDatabase(PostgresFixture fixture, string name)
    {
        _fixture = fixture;
        _name = name;
        ConnectionString = WithDatabase(fixture, name);
    }

    public string ConnectionString { get; }

    public static async Task<IsolatedDatabase> CreateAsync(PostgresFixture fixture)
    {
        await EnsureTemplateAsync(fixture);

        var name = $"isolated_{Guid.NewGuid():N}";
        await ExecuteAsync(fixture, $"CREATE DATABASE \"{name}\" TEMPLATE \"{TemplateName}\"");
        return new IsolatedDatabase(fixture, name);
    }

    public CreatorPlatformDbContext CreateDbContext() => new(Options(ConnectionString));

    public static DbContextOptions<CreatorPlatformDbContext> Options(string connectionString) =>
        new DbContextOptionsBuilder<CreatorPlatformDbContext>()
            .UseNpgsql(connectionString)
            .UseEntityConfigurationsFrom(ApiEntityConfigurations.Assemblies)
            .Options;

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
        await ExecuteAsync(_fixture, $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)");
    }

    private static async Task EnsureTemplateAsync(PostgresFixture fixture)
    {
        await TemplateLock.WaitAsync();
        try
        {
            if (_templateReadyFor == fixture.ConnectionString)
                return;

            await ExecuteAsync(fixture, $"DROP DATABASE IF EXISTS \"{TemplateName}\" WITH (FORCE)");
            await ExecuteAsync(fixture, $"CREATE DATABASE \"{TemplateName}\"");

            var templateConnectionString = WithDatabase(fixture, TemplateName);
            await using (var db = new CreatorPlatformDbContext(Options(templateConnectionString)))
                await db.Database.MigrateAsync();

            // A template must have no open sessions when it is copied.
            NpgsqlConnection.ClearPool(new NpgsqlConnection(templateConnectionString));
            _templateReadyFor = fixture.ConnectionString;
        }
        finally
        {
            TemplateLock.Release();
        }
    }

    private static string WithDatabase(PostgresFixture fixture, string name) =>
        new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;

    private static async Task ExecuteAsync(PostgresFixture fixture, string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
