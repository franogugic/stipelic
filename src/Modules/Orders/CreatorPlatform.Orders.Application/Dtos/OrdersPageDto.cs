namespace CreatorPlatform.Orders.Application.Dtos;

/// <param name="Total">With a period: the orders matching every filter (period, product, page, status, search) — the "Y" of
/// "Showing X of Y"; null without a period.</param>
/// <param name="StatusCounts">With a period: the orders per status in the period + product + page + search, ignoring the
/// status filter (the tabs); null without a period.</param>
public sealed record OrdersPageDto(List<OrderDto> Orders, bool HasMore, int? Total = null, OrderStatusCountsDto? StatusCounts = null);

public sealed record OrderStatusCountsDto(int All, int Paid, int Pending, int Failed, int Refunded);
