using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface ISubscriptionCheckoutSessionService
{
    Task<SubscriptionCheckoutSessionDto> CreateAsync(
        string stripePriceId,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct);
}
