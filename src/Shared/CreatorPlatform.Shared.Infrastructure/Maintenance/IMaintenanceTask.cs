namespace CreatorPlatform.Shared.Infrastructure.Maintenance;

/// <summary>
/// One cleanup job run by <see cref="MaintenanceRunner"/>. Each module owns the tasks for its own tables. A task must be
/// idempotent (it runs every interval, and again after a failure), work in bounded batches, and only touch rows that are
/// past their retention at <c>now</c>.
/// </summary>
public interface IMaintenanceTask
{
    /// <summary>Short kebab-case name for the logs, e.g. <c>sent-email-bodies</c>.</summary>
    string Name { get; }

    /// <summary>Runs the task once; returns how many rows it changed or deleted.</summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken ct);
}
