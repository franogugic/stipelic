using CreatorPlatform.Analytics.Application.Series;

namespace CreatorPlatform.MoneyPath.Tests.Analytics;

public class AnalyticsPeriodPlanTests
{
    private const string Zagreb = "Europe/Zagreb";

    // 11 Oct 2026 14:20 in Zagreb (UTC+2, summer time).
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 20, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Since = new(2025, 3, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Today_IsEveryLocalHourSoFar_ComparedWithYesterdayUpToTheSameTime()
    {
        var plan = AnalyticsPeriodPlan.Create("today", Now, Zagreb, Since);

        Assert.Equal("hour", plan.Step);
        Assert.Equal(15, plan.Buckets.Count); // 00:00 … 14:00
        Assert.Equal(new DateTime(2026, 10, 11, 0, 0, 0), plan.Buckets[0]);
        Assert.Equal(new DateTime(2026, 10, 11, 14, 0, 0), plan.Buckets[^1]);
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 22, 0, 0, TimeSpan.Zero), plan.TotalsFrom); // local midnight
        Assert.Equal(Now, plan.TotalsTo);
        Assert.Equal(24, plan.PreviousBuckets!.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 22, 0, 0, TimeSpan.Zero), plan.PreviousTotalsFrom);
        Assert.Equal(Now.AddDays(-1), plan.PreviousTotalsTo); // yesterday 14:20 local
        Assert.Equal(plan.PreviousTotalsFrom, plan.BucketsFrom);
    }

    [Theory]
    [InlineData("7d", 7)]
    [InlineData("30d", 30)]
    public void Days_AreLocalDays_ComparedWithTheSameNumberOfDaysBefore(string period, int days)
    {
        var plan = AnalyticsPeriodPlan.Create(period, Now, Zagreb, Since);

        Assert.Equal("day", plan.Step);
        Assert.Equal(days, plan.Buckets.Count);
        Assert.Equal(new DateTime(2026, 10, 11), plan.Buckets[^1]);
        Assert.Equal(days, plan.PreviousBuckets!.Count);
        Assert.Equal(plan.Buckets[0].AddDays(-1), plan.PreviousBuckets[^1]);
        Assert.Equal(plan.TotalsFrom, plan.PreviousTotalsTo); // back to back
    }

    [Fact]
    public void All_TotalsCoverAllTime_TheChartTheLast12LocalMonths()
    {
        var plan = AnalyticsPeriodPlan.Create("all", Now, Zagreb, Since);

        Assert.Equal("month", plan.Step);
        Assert.Null(plan.TotalsFrom);
        Assert.Null(plan.PreviousBuckets);
        Assert.Equal(12, plan.Buckets.Count);
        Assert.Equal(new DateTime(2025, 11, 1), plan.Buckets[0]);
        Assert.Equal(new DateTime(2026, 10, 1), plan.Buckets[^1]);
        Assert.Equal(Since, plan.RangeFrom);
    }

    [Fact]
    public void All_ForSomethingNewer_StartsAtItsOwnLocalMonth()
    {
        // 31 Jul 2026 23:30 UTC is already August in Zagreb.
        var plan = AnalyticsPeriodPlan.Create("all", Now, Zagreb, new DateTimeOffset(2026, 7, 31, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal([new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), new DateTime(2026, 10, 1)], plan.Buckets);
    }
}
