namespace CreatorPlatform.Orders.Application.Dtos;

/// <summary>All-time, unfiltered order totals.</summary>
/// <param name="PaidOrderCount">Orders currently Paid (refunded ones are excluded).</param>
/// <param name="TotalPaidAmountCents">Gross revenue: what Paid orders keep (AmountCents minus partial refunds).</param>
/// <param name="Currency">The creator's currency; null when there are no orders at all.</param>
/// <param name="TotalPlatformFeeCents">The platform fees Paid orders keep (less a partial refund's share).</param>
/// <param name="NetAmountCents">TotalPaidAmountCents − TotalPlatformFeeCents.</param>
/// <param name="RefundedOrderCount">Orders with a refund, full or partial.</param>
/// <param name="TotalOrderCount">Every order, any status — the "Y" of "Showing X of Y".</param>
/// <param name="PendingOrderCount">Orders still waiting for payment (Pending).</param>
/// <param name="FailedOrderCount">Orders whose payment failed.</param>
/// <param name="RefundedAmountCents">Sum of RefundedCents — money returned to buyers, full and partial refunds.</param>
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
/// <param name="RefundedCount">Orders with a refund, full or partial, at a latest refund time (UpdatedAt) in the month.</param>
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
/// <param name="TotalPaidAmountCents">Gross revenue: what they keep (AmountCents minus partial refunds).</param>
/// <param name="TotalPlatformFeeCents">The platform fees they keep.</param>
/// <param name="NetAmountCents">TotalPaidAmountCents − TotalPlatformFeeCents.</param>
/// <param name="RefundedOrderCount">Orders with a refund (full or partial) at a latest refund time in the period. An order
/// refunded in two parts in different periods counts both in the later one.</param>
/// <param name="RefundedAmountCents">Their RefundedCents — money returned to buyers.</param>
/// <param name="PendingOrderCount">Orders created in the period that are still waiting for payment.</param>
/// <param name="FailedOrderCount">Orders created in the period whose payment failed.</param>
/// <param name="TotalOrderCount">Paid + refunded + pending + failed above.</param>
/// <param name="Buckets">Oldest first, zero-filled: every hour so far for "today", every day, 7-day weeks for 2m / 3m and
/// months for 6m (anchored at the window's first day, the day after the same date N months ago), or the last 12 months
/// for "all".</param>
/// <param name="PreviousRange">The window before (R6.5); null for "all".</param>
/// <param name="ProductPublicId">The product filter, echoed (R6.5); null for every product.</param>
/// <param name="Previous">The same counts for <paramref name="PreviousRange"/> (R6.5); null for "all".</param>
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
    List<OrderBucketDto> Buckets,
    OrderPeriodRangeDto? PreviousRange = null,
    Guid? ProductPublicId = null,
    OrderPeriodPreviousDto? Previous = null);

/// <summary>The previous window's order counts (R6.5), by the same date rule. No fee.</summary>
public sealed record OrderPeriodPreviousDto(
    int PaidOrderCount,
    long TotalPaidAmountCents,
    int RefundedOrderCount,
    long RefundedAmountCents,
    int PendingOrderCount,
    int FailedOrderCount,
    int TotalOrderCount);

public sealed record OrderPeriodRangeDto(DateTimeOffset From, DateTimeOffset To);

/// <param name="Start">The bucket's local start, "yyyy-MM-ddTHH:mm" in the summary's timezone.</param>
/// <param name="GrossCents">Paid orders paid in the bucket.</param>
/// <param name="NetCents">GrossCents minus their platform fees.</param>
/// <param name="RefundedCount">Orders with a refund (full or partial) at a latest refund time in the bucket.</param>
/// <param name="PaidCount">Paid orders counted in the bucket (R6.5).</param>
/// <param name="RefundedCents">Their RefundedCents (R6.5).</param>
public sealed record OrderBucketDto(string Start, long GrossCents, long NetCents, int RefundedCount, int PaidCount = 0, long RefundedCents = 0);
