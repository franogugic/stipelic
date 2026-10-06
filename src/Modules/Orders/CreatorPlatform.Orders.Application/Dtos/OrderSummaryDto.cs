namespace CreatorPlatform.Orders.Application.Dtos;

/// <summary>All-time, unfiltered order totals.</summary>
/// <param name="PaidOrderCount">Orders currently Paid (refunded ones are excluded).</param>
/// <param name="TotalPaidAmountCents">Gross revenue: sum of AmountCents over Paid orders.</param>
/// <param name="Currency">The creator's currency; null when there are no orders at all.</param>
/// <param name="TotalPlatformFeeCents">Sum of PlatformFeeCents over Paid orders.</param>
/// <param name="NetAmountCents">TotalPaidAmountCents − TotalPlatformFeeCents.</param>
/// <param name="RefundedOrderCount">Orders with status Refunded.</param>
/// <param name="TotalOrderCount">Every order, any status — the "Y" of "Showing X of Y".</param>
public sealed record OrderSummaryDto(
    int PaidOrderCount,
    int TotalPaidAmountCents,
    string? Currency,
    int TotalPlatformFeeCents,
    int NetAmountCents,
    int RefundedOrderCount,
    int TotalOrderCount);
