namespace CreatorPlatform.Products.Application.Dtos;

/// <param name="OrderCount">Every order of the product, any status (R6.5: the orders product filter).</param>
public sealed record ProductRevenueDto(int ProductId, int RevenueCents, int PaidOrderCount, int OrderCount = 0);
