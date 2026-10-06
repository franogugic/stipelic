namespace CreatorPlatform.Payments.Application.Dtos;

public sealed record SubscriptionBillingPeriodDto(DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd);
