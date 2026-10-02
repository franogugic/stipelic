using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface ISubscriptionBillingPeriodService
{
    /// <summary>Reads the current billing period of a provider subscription. Null when the subscription has no
    /// period to report (no items). Throws when the provider can't be reached.</summary>
    Task<SubscriptionBillingPeriodDto?> GetBillingPeriodAsync(string stripeSubscriptionId, CancellationToken ct);
}
