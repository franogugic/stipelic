namespace CreatorPlatform.Orders.Application.Dtos;

public sealed record OrdersPageDto(List<OrderDto> Orders, bool HasMore);
