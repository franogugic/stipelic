using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.Email.Infrastructure.Outbox;
using CreatorPlatform.Integration.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Integration.Tests.Email.Outbox;

/// <summary>EMAIL-1: the outbox settings, their provider defaults and the start-time checks; the throttle's arithmetic.</summary>
public sealed class EmailOutboxOptionsTests
{
    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static EmailOutboxOptions Resolve(string environment, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEmailInfrastructure(configuration, new TestEnvironment(environment));
        services.AddEmailOutboxProcessing(configuration);
        return services.BuildServiceProvider().GetRequiredService<IOptions<EmailOutboxOptions>>().Value;
    }

    [Fact]
    public void Acs_DefaultsToTheCustomDomainQuota()
    {
        var options = Resolve(Environments.Production, new()
        {
            ["Email:ConnectionString"] = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==",
            ["Email:FromAddress"] = "no-reply@luma.test",
        });

        Assert.Equal((30, 100), (options.PerMinute, options.PerHour));
    }

    [Fact]
    public void TheLocalMailpit_GetsAHighQuota_AndAConfiguredQuotaWins()
    {
        Assert.Equal((600, 36_000), (Resolve(Environments.Development, []).PerMinute, Resolve(Environments.Development, []).PerHour));

        var raised = Resolve(Environments.Development, new() { ["Email:Outbox:MaxSendsPerMinute"] = "250", ["Email:Outbox:MaxSendsPerHour"] = "5000" });
        Assert.Equal((250, 5000), (raised.PerMinute, raised.PerHour));
    }

    [Theory]
    [InlineData("Email:Outbox:BatchSize", "500", "more than half")]        // 500 / 8 rounds x 30 s cannot fit a 5-minute lease
    [InlineData("Email:Outbox:MaxSendsPerHour", "10", "MaxSendsPerHour")]   // below the per-minute quota
    [InlineData("Email:Outbox:TransactionalReservePercent", "60", "TransactionalReservePercent")]
    [InlineData("Email:Outbox:MaxParallelSends", "0", "MaxParallelSends")]
    public void AnUnsafeSetting_FailsTheStart(string key, string value, string expected)
    {
        var error = Assert.Throws<OptionsValidationException>(() => Resolve(Environments.Development, new() { [key] = value }));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void TheDefaults_FitTheirLease()
    {
        var options = Resolve(Environments.Development, []);

        Assert.Equal((40, 8, 30, 5), (options.BatchSize, options.MaxParallelSends, options.SendTimeoutSeconds, options.ProcessingLeaseMinutes));
    }

    [Theory]
    [InlineData(30, 20, 30, 24)]   // 6 of 30 kept back
    [InlineData(5, 20, 5, 4)]
    [InlineData(1, 50, 1, 1)]      // a campaign always keeps one permit
    [InlineData(10, 0, 10, 10)]
    public void TheReserve_LeavesCampaignsTheRest(int perMinute, int reservePercent, int transactional, int campaign)
    {
        var throttle = new EmailSendThrottle(
            Options.Create(new EmailOutboxOptions { MaxSendsPerMinute = perMinute, MaxSendsPerHour = 1000, TransactionalReservePercent = reservePercent }),
            new ManualTimeProvider());

        var budget = throttle.GetBudget();

        Assert.Equal((transactional, campaign), (budget.Transactional, budget.Campaign));
    }

    [Fact]
    public void TheMinuteWindow_Slides()
    {
        var clock = new ManualTimeProvider();
        var throttle = new EmailSendThrottle(
            Options.Create(new EmailOutboxOptions { MaxSendsPerMinute = 3, MaxSendsPerHour = 1000, TransactionalReservePercent = 0 }), clock);

        throttle.Record(2);
        clock.Advance(TimeSpan.FromSeconds(30));
        throttle.Record(1);
        var full = throttle.GetBudget();
        clock.Advance(TimeSpan.FromSeconds(30));   // the first two leave the window
        var later = throttle.GetBudget();

        Assert.Equal(0, full.Transactional);
        Assert.Equal(clock.GetUtcNow(), full.NextPermitAt);
        Assert.Equal(2, later.Transactional);
    }
}
