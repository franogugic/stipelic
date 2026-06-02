namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class StartCreatorSubscriptionCheckoutResponseDto
{
    public bool RequiresPayment { get; init; }

    public string PaymentStatus { get; init; } = string.Empty;

    public string? CheckoutUrl { get; init; }
}
