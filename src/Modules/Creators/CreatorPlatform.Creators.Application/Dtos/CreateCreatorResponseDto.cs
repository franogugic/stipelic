namespace CreatorPlatform.Creators.Application.Dtos;

public sealed class CreateCreatorResponseDto
{
    public CreatorResponseDto Creator { get; init; } = new();

    public bool RequiresPayment { get; init; }

    public string PaymentStatus { get; init; } = string.Empty;

    public string? CheckoutUrl { get; init; }
}
