namespace CreatorPlatform.Orders.Application.Dtos;

/// <summary>All-time, unfiltered order totals.</summary>
/// <param name="PaidOrderCount">Orders currently Paid (refunded ones are excluded).</param>
/// <param name="TotalPaidAmountCents">Gross revenue: sum of AmountCents over Paid orders.</param>
/// <param name="Currency">The creator's currency; null when there are no orders at all.</param>
/// <param name="TotalPlatformFeeCents">Sum of PlatformFeeCents over Paid orders.</param>
/// <param name="NetAmountCents">TotalPaidAmountCents − TotalPlatformFeeCents.</param>
/// <param name="RefundedOrderCount">Orders with status Refunded.</param>
/// <param name="TotalOrderCount">Every order, any status — the "Y" of "Showing X of Y".</param>
/// <param name="PendingOrderCount">Orders still waiting for payment (Pending).</param>
/// <param name="FailedOrderCount">Orders whose payment failed.</param>
/// <param name="RefundedAmountCents">Sum of AmountCents over Refunded orders — money returned to buyers.</param>
/// <param name="MonthlyTrend">The last 12 calendar months in the creator's timezone, oldest first, the current month
/// last; every month is present (0 when empty). Empty on the per-landing-page summary.</param>
public sealed record OrderSummaryDto(
    int PaidOrderCount,
    int TotalPaidAmountCents,
    string? Currency,
    int TotalPlatformFeeCents,
    int NetAmountCents,
    int RefundedOrderCount,
    int TotalOrderCount,
    int PendingOrderCount,
    int FailedOrderCount,
    int RefundedAmountCents,
    List<OrderMonthDto> MonthlyTrend);

/// <param name="Month">"yyyy-MM" in the creator's timezone.</param>
/// <param name="GrossCents">Paid orders paid in the month (PaidAt, else CreatedAt).</param>
/// <param name="FeeCents">Their platform fees.</param>
/// <param name="NetCents">GrossCents − FeeCents.</param>
/// <param name="RefundedCount">Orders refunded in the month (the refund's time, the order's UpdatedAt).</param>
public sealed record OrderMonthDto(string Month, long GrossCents, long FeeCents, long NetCents, int RefundedCount);

/// <summary>The order totals of one period, with the period's chart buckets — the answer of
/// <c>GET orders/summary?period=</c>. Every amount and count is for the period only: paid orders by when they were paid
/// (PaidAt, else CreatedAt), refunds by when they were refunded (the order's UpdatedAt), pending and failed orders by
/// when they were created.</summary>
/// <param name="Period">"today" | "7d" | "15d" | "30d" | "2m" | "3m" | "6m" | "all".</param>
/// <param name="Step">"hour" | "day" | "week" | "month" — the bucket size of <paramref name="Buckets"/>.</param>
/// <param name="Timezone">The IANA zone the buckets are in.</param>
/// <param name="Range">The period: the day and long periods end now; "all" starts when the workspace was created.</param>
/// <param name="PaidOrderCount">Orders paid in the period and still Paid.</param>
/// <param name="TotalPaidAmountCents">Gross revenue: their AmountCents.</param>
/// <param name="TotalPlatformFeeCents">Their platform fees.</param>
/// <param name="NetAmountCents">TotalPaidAmountCents − TotalPlatformFeeCents.</param>
/// <param name="RefundedOrderCount">Orders refunded in the period.</param>
/// <param name="RefundedAmountCents">Their AmountCents — money returned to buyers.</param>
/// <param name="PendingOrderCount">Orders created in the period that are still waiting for payment.</param>
/// <param name="FailedOrderCount">Orders created in the period whose payment failed.</param>
/// <param name="TotalOrderCount">Paid + refunded + pending + failed above.</param>
/// <param name="Buckets">Oldest first, zero-filled: every hour so far for "today", every day, 7-day weeks anchored at the
/// window's first day for 2m / 3m / 6m, or the last 12 months for "all".</param>
public sealed record OrderPeriodSummaryDto(
    string Period,
    string Step,
    string Timezone,
    string? Currency,
    OrderPeriodRangeDto Range,
    int PaidOrderCount,
    long TotalPaidAmountCents,
    long TotalPlatformFeeCents,
    long NetAmountCents,
    int RefundedOrderCount,
    long RefundedAmountCents,
    int PendingOrderCount,
    int FailedOrderCount,
    int TotalOrderCount,
    List<OrderBucketDto> Buckets);

public sealed record OrderPeriodRangeDto(DateTimeOffset From, DateTimeOffset To);

/// <param name="Start">The bucket's local start, "yyyy-MM-ddTHH:mm" in the summary's timezone.</param>
/// <param name="GrossCents">Paid orders paid in the bucket.</param>
/// <param name="NetCents">GrossCents minus their platform fees.</param>
/// <param name="RefundedCount">Orders refunded in the bucket.</param>
public sealed record OrderBucketDto(string Start, long GrossCents, long NetCents, int RefundedCount);
