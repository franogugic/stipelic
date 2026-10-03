namespace CreatorPlatform.Orders.Application.Dtos;

/// <summary>Dashboard revenue/views series for one range. Oldest bucket first, the current day/month last; every
/// bucket of the range is present (0 when empty).</summary>
/// <param name="Range">"30d" | "6m" | "12m".</param>
/// <param name="Granularity">"day" for 30d, "month" for 6m / 12m.</param>
public sealed record DashboardTrendsDto(string Range, string Granularity, List<DashboardTrendPointDto> Points);

/// <param name="BucketStart">"yyyy-MM-dd" (UTC) — the day, or the first day of the month.</param>
/// <param name="RevenueCents">Sum of AmountCents of Paid orders paid in the bucket (PaidAt, else CreatedAt).</param>
/// <param name="Views">Page views on the creator's landing pages in the bucket.</param>
public sealed record DashboardTrendPointDto(string BucketStart, long RevenueCents, long Views);
