using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Api.Hosting;

/// <summary>
/// <c>dotnet CreatorPlatform.Api.dll --migrate</c>: applies the pending EF Core migrations and exits, without starting
/// the web server. Production runs it as a one-shot container before the API and the Worker start (see
/// <c>deploy/docker-compose.prod.yml</c>), so <c>Database:AutoMigrate</c> stays <c>false</c> and no serving instance
/// ever changes the schema. It uses the API's own registrations, so the model is the one the API maps (every module's
/// entity configurations) and the configuration is the API's.
/// </summary>
public static class DatabaseMigrationCommand
{
    public const string Switch = "--migrate";

    /// <summary>
    /// Whether <paramref name="args"/> asks for the migration; <paramref name="hostArgs"/> are the arguments without the
    /// switch. The switch must not reach the host: the command-line configuration provider reads a trailing
    /// <c>--name</c> as a key that needs a value and fails the start.
    /// </summary>
    public static bool TryTake(string[] args, out string[] hostArgs)
    {
        hostArgs = args.Where(arg => !string.Equals(arg, Switch, StringComparison.Ordinal)).ToArray();
        return hostArgs.Length != args.Length;
    }

    /// <summary>Applies every pending migration. Returns the process exit code: 0 when the database is up to date
    /// (also when there was nothing to apply), 1 when anything failed. A failure is logged as Critical.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<CreatorPlatformDbContext>().Database;

            var pending = (await database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation(
                    "Database migration: the database is up to date ({LatestMigration}); nothing to apply.",
                    database.GetMigrations().LastOrDefault() ?? "no migrations");
                return 0;
            }

            logger.LogInformation(
                "Database migration: applying {MigrationCount} migration(s): {Migrations}.",
                pending.Count,
                string.Join(", ", pending));

            await database.MigrateAsync(ct);

            logger.LogInformation(
                "Database migration: applied {MigrationCount} migration(s); the database is at {LatestMigration}.",
                pending.Count,
                pending[^1]);
            return 0;
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Database migration failed. The API and the Worker must not start on this schema.");
            return 1;
        }
    }
}
