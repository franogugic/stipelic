namespace CreatorPlatform.Orders.Application.Dtos;

public sealed record PurchasesBucketRow(
    DateTimeOffset BucketStart,
    int PurchaseCount,
    int RevenueCents);
