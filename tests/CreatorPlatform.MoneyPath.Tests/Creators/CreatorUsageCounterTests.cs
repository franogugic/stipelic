using CreatorPlatform.Creators.Domain.Creators;
using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.MoneyPath.Tests.Creators;

public class CreatorUsageCounterTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static CreatorUsageCounter BuildCounter()
    {
        var creator = Creator.Create(1, "Acme", "acme", Currency.Eur, CreatorStatus.Active, "HR", PayoutMode.StripeConnect, Now);
        return CreatorUsageCounter.Create(creator, "max_email_sends_per_month", Now, Now.AddMonths(1), Now);
    }

    [Fact]
    public void TryAddUsage_ExactlyAtLimit_Succeeds()
    {
        var counter = BuildCounter();

        var succeeded = counter.TryAddUsage(500, 500, Now);

        Assert.True(succeeded);
        Assert.Equal(500, counter.UsedValue);
    }

    [Fact]
    public void TryAddUsage_OneOverLimit_FailsAndLeavesStateUnchanged()
    {
        var counter = BuildCounter();
        counter.TryAddUsage(499, 500, Now);

        var succeeded = counter.TryAddUsage(2, 500, Now);

        Assert.False(succeeded);
        Assert.Equal(499, counter.UsedValue);
    }

    [Fact]
    public void TryAddUsage_NegativeLimit_AlwaysSucceedsRegardlessOfAmount()
    {
        var counter = BuildCounter();

        var succeeded = counter.TryAddUsage(1_000_000, -1, Now);

        Assert.True(succeeded);
        Assert.Equal(1_000_000, counter.UsedValue);
    }

    [Fact]
    public void TryAddUsage_AccumulatesAcrossMultipleCalls()
    {
        var counter = BuildCounter();
        counter.TryAddUsage(200, 500, Now);
        counter.TryAddUsage(200, 500, Now);

        var succeeded = counter.TryAddUsage(101, 500, Now);

        Assert.False(succeeded);
        Assert.Equal(400, counter.UsedValue);
    }
}
