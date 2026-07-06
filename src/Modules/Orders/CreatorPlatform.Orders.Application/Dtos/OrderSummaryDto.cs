namespace CreatorPlatform.Orders.Application.Dtos;

public sealed record OrderSummaryDto(
    int PaidOrderCount,
    int TotalPaidAmountCents,
    string? Currency);
