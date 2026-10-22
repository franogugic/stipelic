using CreatorPlatform.Shared.Infrastructure.Redis;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>
/// A one-node Redis Cluster owning all 16384 slots, as on a clustered Azure Managed Redis: the server enforces the
/// cluster rules (a multi-key command or a script must stay in one slot, else CROSSSLOT) and the client runs in
/// cluster mode. The node announces 127.0.0.1 and a fixed host port, so the client can follow the cluster topology
/// from outside the container.
/// </summary>
public sealed class RedisClusterFixture : IAsyncLifetime
{
    private readonly int _hostPort = ClosedPort.Find();
    private readonly IContainer _container;

    public RedisClusterFixture()
    {
        _container = new ContainerBuilder("redis:7.4-alpine")
            .WithPortBinding(_hostPort, 6379)
            .WithCommand(
                "redis-server", "--port", "6379", "--save", "", "--appendonly", "no",
                "--cluster-enabled", "yes",
                "--cluster-announce-ip", "127.0.0.1",
                "--cluster-announce-port", _hostPort.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "ping"))
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var addSlots = await _container.ExecAsync(["redis-cli", "CLUSTER", "ADDSLOTSRANGE", "0", "16383"]);
        if (addSlots.ExitCode != 0)
            throw new InvalidOperationException($"Could not assign the cluster slots: {addSlots.Stderr}");

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var info = await _container.ExecAsync(["redis-cli", "CLUSTER", "INFO"]);
            if (info.Stdout.Contains("cluster_state:ok", StringComparison.Ordinal))
                return;
            await Task.Delay(100);
        }

        throw new InvalidOperationException("The one-node Redis Cluster did not reach cluster_state:ok.");
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public RedisOptions CreateOptions() => new()
    {
        ConnectionString = $"127.0.0.1:{_hostPort},abortConnect=false",
        KeyPrefix = RedisFixture.UniqueKeyPrefix()
    };
}
