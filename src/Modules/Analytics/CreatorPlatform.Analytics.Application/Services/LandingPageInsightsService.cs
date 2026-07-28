using CreatorPlatform.Analytics.Application.Dtos;
using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Orders.Application.Interfaces;

namespace CreatorPlatform.Analytics.Application.Services;

public sealed class LandingPageInsightsService : ILandingPageInsightsService
{
    private readonly IPageViewRepository _pageViewRepository;
    private readonly IEmailCaptureRepository _emailCaptureRepository;
    private readonly IOrderService _orderService;

    public LandingPageInsightsService(
        IPageViewRepository pageViewRepository,
        IEmailCaptureRepository emailCaptureRepository,
        IOrderService orderService)
    {
        _pageViewRepository = pageViewRepository;
        _emailCaptureRepository = emailCaptureRepository;
        _orderService = orderService;
    }

    public async Task<TimeSeriesResponseDto> GetTimeSeriesAsync(
        int landingPageId,
        DateTimeOffset landingPageCreatedAt,
        TimeSeriesPeriod period,
        CancellationToken ct)
    {
        var (cutoff, bucketUnit) = ResolveRange(period, landingPageCreatedAt);

        var views = await _pageViewRepository.GetBucketedViewsAsync(landingPageId, cutoff, bucketUnit, ct);
        var captures = await _emailCaptureRepository.GetBucketedCapturesAsync(landingPageId, cutoff, bucketUnit, ct);
        var purchases = await _orderService.GetBucketedPurchasesAsync(landingPageId, cutoff, bucketUnit, ct);
        var currency = (await _orderService.GetSummaryByLandingPageIdAsync(landingPageId, ct)).Currency;

        // All three sources share the same cutoff/bucketUnit, so bucket timestamps line up 1:1. We still merge
        // by BucketStart (not by index) so a boundary-crossing now() between queries can't shift the alignment.
        var byBucket = new SortedDictionary<DateTimeOffset, MutablePoint>();

        MutablePoint Point(DateTimeOffset bucket)
        {
            if (!byBucket.TryGetValue(bucket, out var point))
            {
                point = new MutablePoint();
                byBucket[bucket] = point;
            }
            return point;
        }

        foreach (var v in views)
        {
            var point = Point(v.BucketStart);
            point.ViewCount = v.ViewCount;
            point.UniqueVisitors = v.UniqueVisitors;
        }

        foreach (var c in captures)
        {
            Point(c.BucketStart).CaptureCount = c.CaptureCount;
        }

        foreach (var p in purchases)
        {
            var point = Point(p.BucketStart);
            point.PurchaseCount = p.PurchaseCount;
            point.RevenueCents = p.RevenueCents;
        }

        var points = byBucket
            .Select(kv => new TimeSeriesPointDto(
                kv.Key,
                kv.Value.ViewCount,
                kv.Value.UniqueVisitors,
                kv.Value.CaptureCount,
                kv.Value.PurchaseCount,
                kv.Value.RevenueCents))
            .ToList();

        return new TimeSeriesResponseDto(period.ToString(), bucketUnit, currency, points);
    }

    // Fixed server-side map: period -> (cutoff, bucketUnit). The bucketUnit literal that reaches SQL always
    // originates here, never from the raw query string.
    private static (DateTimeOffset Cutoff, string BucketUnit) ResolveRange(
        TimeSeriesPeriod period, DateTimeOffset landingPageCreatedAt)
    {
        var now = DateTimeOffset.UtcNow;

        return period switch
        {
            TimeSeriesPeriod.Today       => (now.AddHours(-24),  "hour"),
            TimeSeriesPeriod.Week        => (now.AddDays(-7),    "day"),
            TimeSeriesPeriod.Month       => (now.AddDays(-30),   "day"),
            TimeSeriesPeriod.ThreeMonths => (now.AddDays(-90),   "week"),
            TimeSeriesPeriod.SixMonths   => (now.AddDays(-180),  "week"),
            TimeSeriesPeriod.Year        => (now.AddDays(-365),  "month"),
            TimeSeriesPeriod.AllTime     => (landingPageCreatedAt, "month"),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported time series period."),
        };
    }

    private sealed class MutablePoint
    {
        public long ViewCount { get; set; }
        public long UniqueVisitors { get; set; }
        public long CaptureCount { get; set; }
        public int PurchaseCount { get; set; }
        public int RevenueCents { get; set; }
    }
}
