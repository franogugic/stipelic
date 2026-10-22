using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Application.Series;
using CreatorPlatform.Shared.Application.Analytics;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Application.Time;

namespace CreatorPlatform.Analytics.Application.Services;

public interface IAnalyticsSeriesService
{
    /// <summary><paramref name="scope"/>: "page" | "product" | "workspace" (the whole workspace; <c>id</c> is ignored);
    /// <paramref name="period"/>: one of <see cref="AnalyticsPeriodPlan.Periods"/>. 400 for anything else; 404 when the
    /// workspace, page or product isn't the caller's.</summary>
    Task<AnalyticsSeriesDto> GetAsync(
        string slug, int ownerUserId, string? scope, Guid id, string? period, CancellationToken ct);

    /// <summary>Paid revenue and sales per product for <paramref name="period"/> (default 30d).</summary>
    Task<AnalyticsProductBreakdownDto> GetProductBreakdownAsync(
        string slug, int ownerUserId, string? period, CancellationToken ct);
}

public sealed class AnalyticsSeriesService : IAnalyticsSeriesService
{
    private const string BucketFormat = "yyyy-MM-dd'T'HH:mm";
    private const int MaxListedProducts = 20;

    private readonly IAnalyticsSeriesReader _reader;

    public AnalyticsSeriesService(IAnalyticsSeriesReader reader)
    {
        _reader = reader;
    }

    public async Task<AnalyticsSeriesDto> GetAsync(
        string slug, int ownerUserId, string? scope, Guid id, string? period, CancellationToken ct)
    {
        var scopeKey = scope?.Trim().ToLowerInvariant();
        if (scopeKey is not ("page" or "product" or "workspace"))
            throw new BadRequestException("Invalid scope. Valid values: page, product, workspace.");
        var periodKey = ResolvePeriod(period, "7d");

        // Ownership first: the page or product is looked up inside the caller's own workspace.
        var creator = await _reader.GetCreatorAsync(slug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");

        SeriesItemRow item;
        SeriesTarget target;
        if (scopeKey == "workspace")
        {
            item = new SeriesItemRow(creator.Id, creator.CreatedAt);
            target = new SeriesTarget(await _reader.GetCreatorPageIdsAsync(creator.Id, ct), null, null, null, creator.Id);
        }
        else if (scopeKey == "page")
        {
            item = await _reader.GetPageAsync(creator.Id, id, ct) ?? throw new NotFoundException("Landing page not found.");
            target = new SeriesTarget([item.Id], null, item.Id, null);
        }
        else
        {
            item = await _reader.GetProductAsync(creator.Id, id, ct) ?? throw new NotFoundException("Product not found.");
            target = new SeriesTarget(await _reader.GetProductPageIdsAsync(item.Id, ct), item.Id, null, item.Id);
        }

        var timezone = CreatorTimeZone.Resolve(creator.Timezone);
        var plan = AnalyticsPeriodPlan.Create(periodKey, DateTimeOffset.UtcNow, timezone, item.CreatedAt);
        var current = new SeriesRange(plan.TotalsFrom, plan.TotalsTo);
        var previous = plan.PreviousTotalsFrom is { } previousFrom ? new SeriesRange(previousFrom, plan.PreviousTotalsTo!.Value) : null;

        var read = await _reader.ReadAsync(target, timezone, plan.AllBucketStarts, plan.BucketsFrom, plan.BucketsTo, current, previous, ct);

        var buckets = Fill(plan.Buckets, read.Buckets);
        var previousBuckets = plan.PreviousBuckets is null ? null : Fill(plan.PreviousBuckets, read.Buckets);

        AnalyticsProductExtrasDto? extras = null;
        if (scopeKey == "product")
        {
            var row = await _reader.GetProductExtrasAsync(creator.Id, item.Id, current, ct);
            extras = new AnalyticsProductExtrasDto(row.RefundCount, row.RefundedCents, row.CreatorRevenueCents);
        }

        return new AnalyticsSeriesDto(
            scopeKey,
            periodKey,
            plan.Step,
            timezone,
            creator.Currency,
            item.CreatedAt,
            new AnalyticsRangeDto(plan.RangeFrom, plan.RangeTo),
            plan.PreviousTotalsFrom is { } from ? new AnalyticsRangeDto(from, plan.PreviousRangeTo!.Value) : null,
            ToTotals(read.Current),
            read.Previous is { } previousValues ? ToTotals(previousValues) : null,
            buckets,
            previousBuckets,
            periodKey == "all" ? BestMonth(buckets) : null,
            extras);
    }

    public async Task<AnalyticsProductBreakdownDto> GetProductBreakdownAsync(
        string slug, int ownerUserId, string? period, CancellationToken ct)
    {
        var periodKey = ResolvePeriod(period, "30d");

        var creator = await _reader.GetCreatorAsync(slug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");

        var timezone = CreatorTimeZone.Resolve(creator.Timezone);
        var plan = AnalyticsPeriodPlan.Create(periodKey, DateTimeOffset.UtcNow, timezone, creator.CreatedAt);
        var read = await _reader.GetProductBreakdownAsync(
            creator.Id, new SeriesRange(plan.TotalsFrom, plan.TotalsTo), MaxListedProducts, ct);

        return new AnalyticsProductBreakdownDto(
            periodKey,
            new AnalyticsRangeDto(plan.RangeFrom, plan.RangeTo),
            creator.Currency,
            read.TotalRevenueCents,
            read.Products.Select(p => new AnalyticsProductRevenueDto(p.PublicId, p.Name, p.RevenueCents, p.Sales)).ToList(),
            read.OtherRevenueCents);
    }

    private static string ResolvePeriod(string? period, string fallback)
    {
        var key = string.IsNullOrWhiteSpace(period) ? fallback : period.Trim().ToLowerInvariant();
        if (!AnalyticsPeriodPlan.Periods.Contains(key))
            throw new BadRequestException($"Invalid period. Valid values: {string.Join(", ", AnalyticsPeriodPlan.Periods)}.");
        return key;
    }

    private static List<AnalyticsBucketDto> Fill(IReadOnlyList<DateTime> starts, Dictionary<DateTime, SeriesValues> values) =>
        starts.Select(start =>
        {
            var v = values.GetValueOrDefault(start) ?? new SeriesValues(0, 0, 0, 0);
            return new AnalyticsBucketDto(start.ToString(BucketFormat), v.Views, v.Emails, v.Sales, v.RevenueCents);
        }).ToList();

    private static AnalyticsTotalsDto ToTotals(SeriesValues v) => new(v.Views, v.Emails, v.Sales, v.RevenueCents);

    private static AnalyticsBestMonthDto BestMonth(List<AnalyticsBucketDto> months)
    {
        // The first of equally strong months wins; a metric that is 0 everywhere has no best month.
        AnalyticsMonthValueDto? Best(Func<AnalyticsBucketDto, long> metric)
        {
            var best = months.Aggregate((top, month) => metric(month) > metric(top) ? month : top);
            return metric(best) > 0 ? new AnalyticsMonthValueDto(best.Start[..7], metric(best)) : null;
        }

        return new AnalyticsBestMonthDto(Best(m => m.Views), Best(m => m.Emails), Best(m => m.Sales), Best(m => m.RevenueCents));
    }
}
