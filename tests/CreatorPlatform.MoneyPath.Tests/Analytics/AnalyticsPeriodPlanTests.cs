using CreatorPlatform.Shared.Application.Analytics;

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

    [Fact]
    public void FifteenDays_AreLocalDays_ComparedWithTheFifteenDaysBefore()
    {
        var plan = AnalyticsPeriodPlan.Create("15d", Now, Zagreb, Since);

        Assert.Equal("day", plan.Step);
        Assert.Equal(15, plan.Buckets.Count);
        Assert.Equal(new DateTime(2026, 9, 27), plan.Buckets[0]);
        Assert.Equal(new DateTime(2026, 10, 11), plan.Buckets[^1]);
        Assert.Equal(15, plan.PreviousBuckets!.Count);
        Assert.Equal(new DateTime(2026, 9, 12), plan.PreviousBuckets[0]);
        Assert.Equal(plan.TotalsFrom, plan.PreviousTotalsTo); // back to back
    }

    [Theory]
    [InlineData("2m", 60, 9)]
    [InlineData("3m", 90, 13)]
    [InlineData("6m", 182, 26)]
    public void LongPeriods_AreSevenDayBucketsAnchoredAtTheWindowsFirstDay(string period, int days, int weeks)
    {
        var plan = AnalyticsPeriodPlan.Create(period, Now, Zagreb, Since);

        var firstDay = new DateTime(2026, 10, 11).AddDays(-(days - 1));
        Assert.Equal("week", plan.Step);
        Assert.Equal(weeks, plan.Buckets.Count);
        Assert.Equal(firstDay, plan.Buckets[0]);
        Assert.All(plan.Buckets.Zip(plan.Buckets.Skip(1)), pair => Assert.Equal(pair.First.AddDays(7), pair.Second));
        Assert.True(plan.Buckets[^1] <= new DateTime(2026, 10, 11)); // the last bucket holds today (partial unless days % 7 == 0)
        Assert.True(plan.Buckets[^1].AddDays(7) > new DateTime(2026, 10, 11));
        Assert.Equal(Now, plan.TotalsTo);
        Assert.Equal(Now, plan.RangeTo);
        Assert.Equal(plan.TotalsFrom, plan.RangeFrom);
    }

    [Theory]
    [InlineData("2m", 60)]
    [InlineData("3m", 90)]
    [InlineData("6m", 182)]
    public void LongPeriods_ArePreceededByTheSameNumberOfDays_BucketedTheSameWay(string period, int days)
    {
        var plan = AnalyticsPeriodPlan.Create(period, Now, Zagreb, Since);

        var firstDay = new DateTime(2026, 10, 11).AddDays(-(days - 1));
        Assert.Equal(plan.Buckets.Count, plan.PreviousBuckets!.Count);
        Assert.Equal(firstDay.AddDays(-days), plan.PreviousBuckets[0]);
        Assert.Equal(plan.TotalsFrom, plan.PreviousTotalsTo);   // the windows are back to back
        Assert.Equal(plan.TotalsFrom, plan.PreviousRangeTo);
        // The previous window's last bucket is cut where the current window starts.
        Assert.True(plan.PreviousBuckets[^1] < firstDay);
        Assert.True(plan.PreviousBuckets[^1].AddDays(7) >= firstDay);
        Assert.Equal(plan.PreviousTotalsFrom, plan.BucketsFrom);
    }

    [Fact]
    public void ADstChangeInsideA2mWindow_MovesNoBucket_AndTheBoundariesAreRealLocalMidnights()
    {
        // 27 Oct 2026 12:00 UTC: Zagreb went back to winter time on Sunday 25 Oct, so the window (29 Aug → 27 Oct)
        // starts in summer time (UTC+2) and ends in winter time (UTC+1).
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        var plan = AnalyticsPeriodPlan.Create("2m", now, Zagreb, Since);

        Assert.Equal(new DateTime(2026, 8, 29), plan.Buckets[0]);
        Assert.Equal(new DateTimeOffset(2026, 8, 28, 22, 0, 0, TimeSpan.Zero), plan.TotalsFrom); // 00:00 at UTC+2
        Assert.Equal(new DateTime(2026, 10, 24), plan.Buckets[^1]); // 8 weeks after the 29th, the day before the change
        Assert.All(plan.Buckets, bucket => Assert.Equal(TimeSpan.Zero, bucket.TimeOfDay));
        // Wall-clock edges stay exactly a week apart across the change; no bucket is 167 or 169 hours wide.
        var edges = AnalyticsPeriodPlan.ToEdges(plan.Buckets);
        Assert.All(edges.Zip(edges.Skip(1)), pair => Assert.Equal(7 * 86400, pair.Second - pair.First, precision: 0));
        Assert.Equal(plan.Edges.OrderBy(e => e), plan.Edges);
    }

    [Fact]
    public void A30DayWindow_RunsStraightAcrossAMonthBoundary()
    {
        // 5 Mar 2026: the window starts on 4 Feb, February has 28 days that year.
        var now = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);

        var plan = AnalyticsPeriodPlan.Create("30d", now, Zagreb, Since);

        Assert.Equal(30, plan.Buckets.Count);
        Assert.Equal(new DateTime(2026, 2, 4), plan.Buckets[0]);
        Assert.Equal(new DateTime(2026, 2, 28), plan.Buckets[24]);
        Assert.Equal(new DateTime(2026, 3, 1), plan.Buckets[25]);
        Assert.Equal(new DateTime(2026, 3, 5), plan.Buckets[^1]);
        Assert.Equal(new DateTime(2026, 1, 5), plan.PreviousBuckets![0]);
        Assert.Equal(new DateTime(2026, 2, 3), plan.PreviousBuckets[^1]);
    }

    [Theory]
    [InlineData("today")]
    [InlineData("7d")]
    [InlineData("15d")]
    [InlineData("30d")]
    [InlineData("2m")]
    [InlineData("3m")]
    [InlineData("6m")]
    public void TheBucketStartsOfBothPeriods_AreOneAscendingRun_SoOneWidthBucketBinsEveryEvent(string period)
    {
        var plan = AnalyticsPeriodPlan.Create(period, Now, Zagreb, Since);

        var all = plan.AllBucketStarts;
        Assert.Equal(plan.PreviousBuckets!.Count + plan.Buckets.Count, all.Count);
        Assert.All(all.Zip(all.Skip(1)), pair => Assert.True(pair.First < pair.Second));
        Assert.Equal(all.Count, plan.Edges.Length);
    }

    [Fact]
    public void All_HasNoPreviousPeriod_AndItsBucketsAreTheOnlyEdges()
    {
        var plan = AnalyticsPeriodPlan.Create("all", Now, Zagreb, Since);

        Assert.Null(plan.PreviousBuckets);
        Assert.Equal(plan.Buckets, plan.AllBucketStarts);
    }

    [Fact]
    public void ThePeriodList_IsTheEightPeriodsOfTheMenu()
    {
        Assert.Equal(["today", "7d", "15d", "30d", "2m", "3m", "6m", "all"], AnalyticsPeriodPlan.Periods);
    }

    [Fact]
    public void AnUnknownPeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AnalyticsPeriodPlan.Create("90d", Now, Zagreb, Since));
    }
}
