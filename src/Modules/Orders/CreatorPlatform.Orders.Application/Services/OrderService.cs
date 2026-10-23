using System.Runtime.CompilerServices;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Analytics;
using CreatorPlatform.Shared.Application.Exceptions;
using CreatorPlatform.Shared.Application.Time;

namespace CreatorPlatform.Orders.Application.Services;

public sealed class OrderService : IOrderService
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 100;
    private const int MaxSearchLength = 100;
    private const int ExportBatchSize = 500;

    private readonly IOrderRepository _orderRepository;
    private readonly IHomeSummaryCache _homeSummaryCache;
    private readonly IDashboardTrendsCache _dashboardTrendsCache;

    public OrderService(
        IOrderRepository orderRepository,
        IHomeSummaryCache homeSummaryCache,
        IDashboardTrendsCache dashboardTrendsCache)
    {
        _orderRepository = orderRepository;
        _homeSummaryCache = homeSummaryCache;
        _dashboardTrendsCache = dashboardTrendsCache;
    }

    public async Task<OrdersPageDto> ListAsync(
        string creatorSlug,
        int ownerUserId,
        OrderListQuery query,
        DateTimeOffset? afterCreatedAt,
        DateTimeOffset? afterCountedAt,
        Guid? afterId,
        int limit,
        CancellationToken ct)
    {
        var clampedLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var filter = await ResolveFilterAsync(creatorSlug, ownerUserId, query, ct);
        var cursorAt = filter.Period is null ? afterCreatedAt : afterCountedAt;
        var cursor = cursorAt is { } at && afterId is { } id ? new OrderListCursor(at, id) : null;

        // Fetch one extra row to detect a next page without a separate COUNT query, then trim it.
        var rows = await _orderRepository.GetByCreatorSlugAsync(creatorSlug, ownerUserId, filter, cursor, clampedLimit + 1, ct);

        var hasMore = rows.Count > clampedLimit;
        var page = hasMore ? rows.Take(clampedLimit).ToList() : rows;
        if (filter.Period is null)
            return new OrdersPageDto(page, hasMore);

        // With a period: the tabs (every status) and the total of the current tab, from one grouped count.
        var counts = await _orderRepository.GetStatusCountsAsync(creatorSlug, ownerUserId, filter, ct);
        var total = filter.Status switch
        {
            OrderStatus.Paid => counts.Paid,
            OrderStatus.Pending => counts.Pending,
            OrderStatus.Failed => counts.Failed,
            OrderStatus.Refunded => counts.Refunded,
            _ => counts.All,
        };
        return new OrdersPageDto(page, hasMore, total, counts);
    }

    public async Task<OrdersExport> StartExportAsync(
        string creatorSlug,
        int ownerUserId,
        OrderListQuery query,
        CancellationToken ct)
    {
        var filter = await ResolveFilterAsync(creatorSlug, ownerUserId, query, ct);

        return new OrdersExport(creatorSlug, StreamOrdersAsync(creatorSlug, ownerUserId, filter, ct));
    }

    /// <summary>Validates the query (400) and the ownership of the workspace and the product (404), and turns the period
    /// into its counted-date window in the creator's timezone.</summary>
    private async Task<OrderListFilter> ResolveFilterAsync(string creatorSlug, int ownerUserId, OrderListQuery query, CancellationToken ct)
    {
        var status = ParseStatus(query.Status);
        var customerSearch = NormalizeSearch(query.Search);
        var periodKey = query.Period is null ? null : ResolvePeriod(query.Period);

        var context = await _orderRepository.GetPeriodContextAsync(creatorSlug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");
        if (query.ProductId is { } productId && await _orderRepository.GetProductIdAsync(context.CreatorId, productId, ct) is null)
            throw new NotFoundException("Product not found.");

        OrderCountedRange? period = null;
        if (periodKey is not null)
        {
            var plan = AnalyticsPeriodPlan.Create(periodKey, DateTimeOffset.UtcNow, context.Timezone, context.CreatedAt);
            period = new OrderCountedRange(plan.TotalsFrom, plan.TotalsTo);
        }

        return new OrderListFilter(query.ProductId, query.LandingPageId, status, customerSearch, period);
    }

    // The list's own keyset query, batch after batch, newest first (by counted date with a period, else by CreatedAt),
    // one batch in memory at a time.
    private async IAsyncEnumerable<OrderDto> StreamOrdersAsync(
        string creatorSlug,
        int ownerUserId,
        OrderListFilter filter,
        [EnumeratorCancellation] CancellationToken ct)
    {
        OrderListCursor? cursor = null;

        while (true)
        {
            var rows = await _orderRepository.GetByCreatorSlugAsync(creatorSlug, ownerUserId, filter, cursor, ExportBatchSize + 1, ct);
            var hasMore = rows.Count > ExportBatchSize;

            foreach (var order in hasMore ? rows.Take(ExportBatchSize) : rows)
            {
                cursor = new OrderListCursor(filter.Period is null ? order.CreatedAt : order.CountedAt, order.PublicId);
                yield return order;
            }

            if (!hasMore)
                yield break;
        }
    }

    private static string ResolvePeriod(string period)
    {
        var key = period.Trim().ToLowerInvariant();
        if (!AnalyticsPeriodPlan.Periods.Contains(key))
            throw new BadRequestException($"Invalid period. Valid values: {string.Join(", ", AnalyticsPeriodPlan.Periods)}.");
        return key;
    }

    /// <summary>Trimmed customer search term; null when blank. Longer than 100 characters is rejected.</summary>
    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return null;

        var trimmed = search.Trim();
        if (trimmed.Length > MaxSearchLength)
            throw new BadRequestException($"Search cannot be longer than {MaxSearchLength} characters.");

        return trimmed;
    }

    private static OrderStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            throw new BadRequestException($"Invalid status. Valid values: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");

        return parsed;
    }

    private sealed record TrendRange(string Unit, int Buckets);

    private static readonly IReadOnlyDictionary<string, TrendRange> TrendRanges = new Dictionary<string, TrendRange>
    {
        ["30d"] = new("day", 30),
        ["6m"] = new("month", 6),
        ["12m"] = new("month", 12),
    };

    public async Task<DashboardTrendsDto> GetDashboardTrendsAsync(
        string creatorSlug, int ownerUserId, string? range, CancellationToken ct)
    {
        var rangeKey = string.IsNullOrWhiteSpace(range) ? "30d" : range.Trim().ToLowerInvariant();
        if (!TrendRanges.TryGetValue(rangeKey, out var trendRange))
            throw new BadRequestException("Invalid range. Valid values: 30d, 6m, 12m.");

        // Ownership first, cache second: the cache is keyed by the internal creator id and is never consulted for a
        // caller who doesn't own the workspace.
        var creatorId = await _orderRepository.GetCreatorIdForOwnerAsync(creatorSlug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");

        if (await _dashboardTrendsCache.GetAsync(creatorId, rangeKey, ct) is { } cached)
            return cached;

        // Days and months are the creator's own (creator timezone); the bounds are the instants they start.
        var timezone = await _orderRepository.GetCreatorTimezoneAsync(creatorId, ct);
        var local = CreatorTimeZone.LocalNow(DateTimeOffset.UtcNow, timezone);
        DateTime lastLocal, firstLocal;
        if (trendRange.Unit == "day")
        {
            lastLocal = local.Date;
            firstLocal = lastLocal.AddDays(-(trendRange.Buckets - 1));
        }
        else
        {
            lastLocal = new DateTime(local.Year, local.Month, 1);
            firstLocal = lastLocal.AddMonths(-(trendRange.Buckets - 1));
        }
        var firstBucket = CreatorTimeZone.ToInstant(firstLocal, timezone);
        var lastBucket = CreatorTimeZone.ToInstant(lastLocal, timezone);

        // Sequential: both use the request's single DbContext.
        var revenue = await _orderRepository.GetRevenueTrendAsync(creatorId, trendRange.Unit, firstBucket, lastBucket, timezone, ct);
        var views = await _orderRepository.GetViewsTrendAsync(creatorId, trendRange.Unit, firstBucket, lastBucket, timezone, ct);
        var viewsByBucket = views.ToDictionary(v => v.BucketStart, v => v.Value);

        var result = new DashboardTrendsDto(
            rangeKey,
            trendRange.Unit,
            revenue
                .Select(r => new DashboardTrendPointDto(r.BucketStart, r.Value, viewsByBucket.GetValueOrDefault(r.BucketStart)))
                .ToList());

        await _dashboardTrendsCache.SetAsync(creatorId, rangeKey, result, ct);
        return result;
    }

    public Task<OrderSummaryDto> GetSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetSummaryByCreatorSlugAsync(creatorSlug, ownerUserId, ct);
    }

    public async Task<OrderPeriodSummaryDto> GetPeriodSummaryAsync(
        string creatorSlug, int ownerUserId, string period, CancellationToken ct, Guid? productId = null)
    {
        var periodKey = ResolvePeriod(period);

        var context = await _orderRepository.GetPeriodContextAsync(creatorSlug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");
        int? productInternalId = null;
        if (productId is { } publicId)
            productInternalId = await _orderRepository.GetProductIdAsync(context.CreatorId, publicId, ct)
                ?? throw new NotFoundException("Product not found.");

        var plan = AnalyticsPeriodPlan.Create(periodKey, DateTimeOffset.UtcNow, context.Timezone, context.CreatedAt);
        var read = await _orderRepository.GetPeriodSummaryAsync(context.CreatorId, context.Timezone, plan, ct, productInternalId);

        var totals = read.Totals;
        var buckets = plan.Buckets
            .Select(start =>
            {
                var values = read.Buckets.GetValueOrDefault(start) ?? new OrderBucketValues(0, 0, 0);
                return new OrderBucketDto(
                    start.ToString("yyyy-MM-dd'T'HH:mm"), values.GrossCents, values.GrossCents - values.FeeCents, values.RefundedCount,
                    values.PaidCount, values.RefundedCents);
            })
            .ToList();

        return new OrderPeriodSummaryDto(
            periodKey,
            plan.Step,
            context.Timezone,
            context.Currency,
            new OrderPeriodRangeDto(plan.RangeFrom, plan.RangeTo),
            totals.PaidOrderCount,
            totals.TotalPaidAmountCents,
            totals.TotalPlatformFeeCents,
            totals.TotalPaidAmountCents - totals.TotalPlatformFeeCents,
            totals.RefundedOrderCount,
            totals.RefundedAmountCents,
            totals.PendingOrderCount,
            totals.FailedOrderCount,
            totals.PaidOrderCount + totals.RefundedOrderCount + totals.PendingOrderCount + totals.FailedOrderCount,
            buckets,
            plan.PreviousTotalsFrom is { } previousFrom ? new OrderPeriodRangeDto(previousFrom, plan.PreviousRangeTo!.Value) : null,
            productId,
            read.Previous is { } previous
                ? new OrderPeriodPreviousDto(
                    previous.PaidOrderCount, previous.TotalPaidAmountCents, previous.RefundedOrderCount, previous.RefundedAmountCents,
                    previous.PendingOrderCount, previous.FailedOrderCount,
                    previous.PaidOrderCount + previous.RefundedOrderCount + previous.PendingOrderCount + previous.FailedOrderCount)
                : null);
    }

    public Task<LandingPageSalesByPeriodDto> GetSalesByPeriodForLandingPageAsync(
        int landingPageId, StatsPeriods periods, CancellationToken ct)
    {
        return _orderRepository.GetSalesByPeriodForLandingPageAsync(landingPageId, periods, ct);
    }

    public Task<List<LandingPageOrdersSummaryDto>> GetOrdersSummaryByCreatorGroupedByLandingPageAsync(
        string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetOrdersSummaryByCreatorGroupedByLandingPageAsync(creatorSlug, ownerUserId, ct);
    }

    public async Task InvalidateHomeSummaryAsync(string creatorSlug, int ownerUserId)
    {
        // CancellationToken.None: the caller's change is already committed, so an aborted request must not leave
        // a stale summary behind.
        if (await _orderRepository.GetCreatorIdForOwnerAsync(creatorSlug, ownerUserId, CancellationToken.None) is int creatorId)
            await _homeSummaryCache.RemoveAsync(creatorId, CancellationToken.None);
    }

    public async Task<HomeSummaryDto> GetHomeSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        // Ownership first: the cache is keyed by the internal id, so it is only touched for the caller's own workspace.
        var creatorId = await _orderRepository.GetCreatorIdForOwnerAsync(creatorSlug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");

        if (await _homeSummaryCache.GetAsync(creatorId, ct) is { } cached)
            return cached;

        var result = await _orderRepository.GetHomeSummaryByCreatorIdAsync(creatorId, ct);

        await _homeSummaryCache.SetAsync(creatorId, result, ct);

        return result;
    }
}
