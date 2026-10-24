namespace CreatorPlatform.Orders.Application.Dtos;

/// <param name="MonthlyPaidOrderCounts">Paid orders per calendar month for the last 12 months in the creator's
/// timezone (by PaidAt, else CreatedAt), oldest first, the current month last.</param>
/// <param name="CustomerCount">Distinct buyer emails over Paid orders, all time.</param>
/// <param name="RefundedOrderCount">Orders with a refund, full or partial, all time.</param>
/// <param name="RefundedAmountCents">Sum of RefundedCents, all time.</param>
public sealed record HomeSummaryDto(
    int TotalPaidAmountCents,
    int PaidOrderCount,
    string? Currency,
    int ProductCount,
    int LandingPageCount,
    List<OrderDto> RecentOrders,
    int ThisMonthRevenueCents,
    TopProductDto? TopProduct,
    List<int> RevenueTrend,
    int EmailsSentThisMonth,
    int EmailsMonthlyLimit,
    int TotalPageViews,
    int SubscriberCount,
    List<int> ViewsTrend,
    List<int> MonthlyRevenueTrend,
    int TotalPlatformFeeCents,
    List<int> MonthlyPaidOrderCounts,
    int CustomerCount,
    int RefundedOrderCount,
    int RefundedAmountCents);

public sealed record TopProductDto(string Name, int TotalCents);
