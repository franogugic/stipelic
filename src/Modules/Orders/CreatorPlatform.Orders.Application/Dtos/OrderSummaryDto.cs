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
