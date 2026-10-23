namespace CreatorPlatform.Shared.Infrastructure.Maintenance;

/// <summary>The <c>Maintenance</c> section: how often the Worker's cleanup runs and how long each kind of row is kept.</summary>
public sealed class MaintenanceOptions
{
    public const string SectionName = "Maintenance";

    /// <summary>Off only for a Worker that must not clean up (e.g. a second Worker pointed at someone else's database).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Time between two runs. Every task only touches rows past their age, so running often is harmless.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>The first run waits this long after the Worker starts.</summary>
    public int StartDelaySeconds { get; set; } = 60;

    /// <summary>Rows changed per statement: bounds lock time and transaction size on big tables.</summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>A sent email's HTML and text bodies are dropped this long after sending; the row stays.</summary>
    public int SentEmailBodyRetentionDays { get; set; } = 30;

    /// <summary>Sessions and one-time tokens are deleted this long after they expired (sessions also after revocation).</summary>
    public int ExpiredAuthRecordRetentionDays { get; set; } = 30;

    public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);

    public TimeSpan StartDelay => TimeSpan.FromSeconds(StartDelaySeconds);
}
