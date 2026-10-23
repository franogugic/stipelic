using CreatorPlatform.Api.Hosting;
using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Maintenance;

/// <summary>U2: the runner runs every task, isolates a failing one, and lets only one Worker instance run at a time
/// (Postgres advisory lock).</summary>
[Collection(PostgresCollection.Name)]
public sealed class MaintenanceRunnerTests
{
    private readonly PostgresFixture _postgres;

    public MaintenanceRunnerTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private sealed class CountingTask(string name, int affected) : IMaintenanceTask
    {
        public int Runs { get; private set; }

        public string Name => name;

        public Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
        {
            Runs++;
            return Task.FromResult(affected);
        }
    }

    private sealed class FailingTask : IMaintenanceTask
    {
        public string Name => "always-fails";

        public Task<int> RunAsync(DateTimeOffset now, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }

    /// <summary>Holds the run (and so the lock) until released.</summary>
    private sealed class BlockingTask : IMaintenanceTask
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "blocking";

        public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(ct);
            return 0;
        }
    }

    private (MaintenanceRunner Runner, ListLogger<MaintenanceRunner> Logger, ServiceProvider Services) Runner(
        params IMaintenanceTask[] tasks)
    {
        var services = new ServiceCollection();
        services.AddDbContext<CreatorPlatformDbContext>(options => options
            .UseNpgsql(_postgres.ConnectionString)
            .UseEntityConfigurationsFrom(ApiEntityConfigurations.Assemblies));
        services.AddMaintenance(new ConfigurationBuilder().Build());
        var logger = new ListLogger<MaintenanceRunner>();
        services.AddSingleton<ILogger<MaintenanceRunner>>(logger);
        foreach (var task in tasks)
            services.AddSingleton(task);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<MaintenanceRunner>(), logger, provider);
    }

    [Fact]
    public async Task EveryTaskRuns_AndAFailingTask_DoesNotStopTheOthers()
    {
        var first = new CountingTask("first", affected: 3);
        var last = new CountingTask("last", affected: 0);
        var (runner, logger, services) = Runner(first, new FailingTask(), last);
        await using var _ = services;

        var result = await runner.RunOnceAsync(CancellationToken.None);

        Assert.True(result.Ran);
        Assert.Equal(
            [new MaintenanceTaskResult("first", 3, true), new MaintenanceTaskResult("always-fails", 0, false), new MaintenanceTaskResult("last", 0, true)],
            result.Tasks);
        Assert.Equal((1, 1), (first.Runs, last.Runs));
        var error = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains("always-fails", error.Message);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("first: 3 row(s)"));
    }

    [Fact]
    public async Task WhileOneInstanceRuns_AnotherSkips_AndRunsAgainOnceTheLockIsFree()
    {
        var blocking = new BlockingTask();
        var (holder, _, holderServices) = Runner(blocking);
        var other = new CountingTask("other", affected: 0);
        var (second, _, secondServices) = Runner(other);
        await using var _1 = holderServices;
        await using var _2 = secondServices;

        var holding = holder.RunOnceAsync(CancellationToken.None);
        await blocking.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var whileHeld = await second.RunOnceAsync(CancellationToken.None);

        Assert.False(whileHeld.Ran);
        Assert.Equal(0, other.Runs);

        blocking.Release.SetResult();
        Assert.True((await holding).Ran);

        var afterwards = await second.RunOnceAsync(CancellationToken.None);

        Assert.True(afterwards.Ran);
        Assert.Equal(1, other.Runs);
    }

    [Theory]
    [InlineData("IntervalMinutes", "4")]
    [InlineData("BatchSize", "5")]
    [InlineData("SentEmailBodyRetentionDays", "0")]
    [InlineData("ExpiredAuthRecordRetentionDays", "0")]
    [InlineData("StartDelaySeconds", "-1")]
    public void AnUnsafeSetting_FailsTheStart(string key, string value)
    {
        var services = new ServiceCollection();
        services.AddMaintenance(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"Maintenance:{key}"] = value })
            .Build());
        using var provider = services.BuildServiceProvider();

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<MaintenanceOptions>>().Value);
        Assert.Contains($"Maintenance:{key}", error.Message);
    }

    [Fact]
    public void TheDefaults_AreValid()
    {
        var services = new ServiceCollection();
        services.AddMaintenance(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<MaintenanceOptions>>().Value;

        Assert.Equal((true, 60, 30, 30), (options.Enabled, options.IntervalMinutes, options.SentEmailBodyRetentionDays, options.ExpiredAuthRecordRetentionDays));
        Assert.Equal((60, 25), (options.AbandonedUploadMinutes, options.StalePendingOrderHours));
    }
}
