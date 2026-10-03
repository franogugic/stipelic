using System.Runtime.CompilerServices;
using CreatorPlatform.Orders.Application.Dtos;
using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Shared.Application.Analytics;
using CreatorPlatform.Shared.Application.Exceptions;

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
        Guid? productId,
        Guid? landingPageId,
        string? status,
        string? search,
        DateTimeOffset? afterCreatedAt,
        Guid? afterId,
        int limit,
        CancellationToken ct)
    {
        var clampedLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var parsedStatus = ParseStatus(status);
        var customerSearch = NormalizeSearch(search);

        // Fetch one extra row to detect a next page without a separate COUNT query, then trim it.
        var rows = await _orderRepository.GetByCreatorSlugAsync(
            creatorSlug, ownerUserId, productId, landingPageId, parsedStatus, customerSearch, afterCreatedAt, afterId,
            clampedLimit + 1, ct);

        var hasMore = rows.Count > clampedLimit;
        var page = hasMore ? rows.Take(clampedLimit).ToList() : rows;

        return new OrdersPageDto(page, hasMore);
    }

    public async Task<OrdersExport> StartExportAsync(
        string creatorSlug,
        int ownerUserId,
        Guid? productId,
        Guid? landingPageId,
        string? status,
        string? search,
        CancellationToken ct)
    {
        var parsedStatus = ParseStatus(status);
        var customerSearch = NormalizeSearch(search);

        if (!await _orderRepository.CreatorExistsForOwnerAsync(creatorSlug, ownerUserId, ct))
            throw new NotFoundException("Creator workspace not found.");

        return new OrdersExport(
            creatorSlug,
            StreamOrdersAsync(creatorSlug, ownerUserId, productId, landingPageId, parsedStatus, customerSearch, ct));
    }

    // The list's own keyset query, batch after batch: (CreatedAt, PublicId) cursor, newest first, one batch in
    // memory at a time.
    private async IAsyncEnumerable<OrderDto> StreamOrdersAsync(
        string creatorSlug,
        int ownerUserId,
        Guid? productId,
        Guid? landingPageId,
        OrderStatus? status,
        string? customerSearch,
        [EnumeratorCancellation] CancellationToken ct)
    {
        DateTimeOffset? afterCreatedAt = null;
        Guid? afterId = null;

        while (true)
        {
            var rows = await _orderRepository.GetByCreatorSlugAsync(
                creatorSlug, ownerUserId, productId, landingPageId, status, customerSearch, afterCreatedAt, afterId,
                ExportBatchSize + 1, ct);
            var hasMore = rows.Count > ExportBatchSize;

            foreach (var order in hasMore ? rows.Take(ExportBatchSize) : rows)
            {
                afterCreatedAt = order.CreatedAt;
                afterId = order.PublicId;
                yield return order;
            }

            if (!hasMore)
                yield break;
        }
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

        if (_dashboardTrendsCache.TryGet(creatorId, rangeKey, out var cached) && cached is not null)
            return cached;

        var now = DateTimeOffset.UtcNow;
        DateTimeOffset lastBucket, firstBucket;
        if (trendRange.Unit == "day")
        {
            lastBucket = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
            firstBucket = lastBucket.AddDays(-(trendRange.Buckets - 1));
        }
        else
        {
            lastBucket = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            firstBucket = lastBucket.AddMonths(-(trendRange.Buckets - 1));
        }

        // Sequential: both use the request's single DbContext.
        var revenue = await _orderRepository.GetRevenueTrendAsync(creatorId, trendRange.Unit, firstBucket, lastBucket, ct);
        var views = await _orderRepository.GetViewsTrendAsync(creatorId, trendRange.Unit, firstBucket, lastBucket, ct);
        var viewsByBucket = views.ToDictionary(v => v.BucketStart, v => v.Value);

        var result = new DashboardTrendsDto(
            rangeKey,
            trendRange.Unit,
            revenue
                .Select(r => new DashboardTrendPointDto(r.BucketStart, r.Value, viewsByBucket.GetValueOrDefault(r.BucketStart)))
                .ToList());

        _dashboardTrendsCache.Set(creatorId, rangeKey, result);
        return result;
    }

    public Task<OrderSummaryDto> GetSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        return _orderRepository.GetSummaryByCreatorSlugAsync(creatorSlug, ownerUserId, ct);
    }

    public Task<OrderSummaryDto> GetSummaryByLandingPageIdAsync(int landingPageId, CancellationToken ct)
    {
        return _orderRepository.GetSummaryByLandingPageIdAsync(landingPageId, ct);
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

    public Task<List<PurchasesBucketRow>> GetBucketedPurchasesAsync(int landingPageId, DateTimeOffset cutoff, string bucketUnit, CancellationToken ct)
    {
        return _orderRepository.GetBucketedPurchasesAsync(landingPageId, cutoff, bucketUnit, ct);
    }

    public async Task InvalidateHomeSummaryAsync(string creatorSlug, int ownerUserId)
    {
        // CancellationToken.None: the caller's change is already committed, so an aborted request must not leave
        // a stale summary behind.
        if (await _orderRepository.GetCreatorIdForOwnerAsync(creatorSlug, ownerUserId, CancellationToken.None) is int creatorId)
            _homeSummaryCache.Remove(creatorId);
    }

    public async Task<HomeSummaryDto> GetHomeSummaryAsync(string creatorSlug, int ownerUserId, CancellationToken ct)
    {
        // Ownership first: the cache is keyed by the internal id, so it is only touched for the caller's own workspace.
        var creatorId = await _orderRepository.GetCreatorIdForOwnerAsync(creatorSlug, ownerUserId, ct)
            ?? throw new NotFoundException("Creator workspace not found.");

        if (_homeSummaryCache.TryGet(creatorId, out var cached) && cached is not null)
            return cached;

        var result = await _orderRepository.GetHomeSummaryByCreatorIdAsync(creatorId, ct);

        _homeSummaryCache.Set(creatorId, result);

        return result;
    }
}
