namespace CreatorPlatform.Orders.Application.Interfaces;

public sealed record CreateCheckoutResultDto(string CheckoutUrl);

public interface IOrderCheckoutService
{
    Task<CreateCheckoutResultDto> CreateCheckoutAsync(
        string creatorSlug,
        string landingPageSlug,
        string email,
        CancellationToken ct);
}
