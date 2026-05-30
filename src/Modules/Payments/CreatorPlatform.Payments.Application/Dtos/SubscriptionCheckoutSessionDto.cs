namespace CreatorPlatform.Payments.Application.Dtos;

public sealed class SubscriptionCheckoutSessionDto
{
    public string ProviderCheckoutSessionId { get; init; } = string.Empty;

    public string CheckoutUrl { get; init; } = string.Empty;
}
