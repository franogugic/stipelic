namespace CreatorPlatform.Orders.Application.Dtos;

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
    int EmailsMonthlyLimit);

public sealed record TopProductDto(string Name, int TotalCents);
