namespace CreatorPlatform.Products.Application.Dtos;

/// <summary>Everything the product detail screen shows except the latest orders (those come from the orders list).</summary>
/// <param name="RevenueCents">Paid orders of the product, all time.</param>
/// <param name="SalesCount">Paid orders of the product, all time.</param>
/// <param name="ThisMonthRevenueCents">Paid orders since the start of the current UTC month.</param>
/// <param name="ContactCount">Distinct contacts captured on a landing page of this product, or captured with this product directly.</param>
/// <param name="Granularity">"day" for 30d, "month" for 3m / 6m / 1y.</param>
/// <param name="Points">Revenue per bucket, oldest first, the current day/month last; every bucket is present (0 when empty).</param>
public sealed record ProductAnalyticsDto(
    long RevenueCents,
    int SalesCount,
    long ThisMonthRevenueCents,
    int ContactCount,
    string Granularity,
    List<ProductRevenuePointDto> Points,
    List<ProductSellingPageDto> SellingPages);

/// <param name="BucketStart">"yyyy-MM-dd" (UTC) — the day, or the first day of the month.</param>
public sealed record ProductRevenuePointDto(string BucketStart, long RevenueCents);

public sealed record ProductSellingPageDto(Guid PublicId, string Title, string Status);

/// <summary>The orders-side numbers of the product analytics, in one read.</summary>
public sealed record ProductOrderStatsDto(
    long RevenueCents,
    int SalesCount,
    long ThisMonthRevenueCents,
    List<ProductRevenuePointDto> Points);
