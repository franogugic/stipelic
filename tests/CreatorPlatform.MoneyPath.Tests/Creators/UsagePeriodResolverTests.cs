using CreatorPlatform.Creators.Application.Interfaces;

namespace CreatorPlatform.MoneyPath.Tests.Creators;

public class UsagePeriodResolverTests
{
    [Fact]
    public void Resolve_CalendarMonth_StartsAtFirstOfMonthUtcMidnight()
    {
        var now = new DateTimeOffset(2026, 7, 19, 14, 32, 0, TimeSpan.Zero);

        var (start, end) = UsagePeriodResolver.Resolve(UsagePeriod.CalendarMonth, now);

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void Resolve_CalendarMonth_DifferentMonths_ProduceDifferentPeriods()
    {
        var july = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        var august = new DateTimeOffset(2026, 8, 1, 0, 0, 1, TimeSpan.Zero);

        var julyPeriod = UsagePeriodResolver.Resolve(UsagePeriod.CalendarMonth, july);
        var augustPeriod = UsagePeriodResolver.Resolve(UsagePeriod.CalendarMonth, august);

        Assert.NotEqual(julyPeriod, augustPeriod);
    }

    [Fact]
    public void Resolve_AllTime_IgnoresNowAndReturnsFixedSentinels()
    {
        var periodA = UsagePeriodResolver.Resolve(UsagePeriod.AllTime, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var periodB = UsagePeriodResolver.Resolve(UsagePeriod.AllTime, new DateTimeOffset(2030, 6, 15, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(periodA, periodB);
        Assert.Equal(new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.Zero), periodA.Start);
        Assert.Equal(new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.Zero), periodA.End);
    }
}
