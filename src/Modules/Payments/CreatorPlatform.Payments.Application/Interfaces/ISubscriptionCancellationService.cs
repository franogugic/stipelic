namespace CreatorPlatform.Payments.Application.Interfaces;

public interface ISubscriptionCancellationService
{
    Task CancelAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct);

    /// <summary>Cancels the subscription in Stripe right now (no proration, no final invoice). A subscription that is
    /// already cancelled in Stripe counts as done. Any other Stripe failure throws, so the caller changes nothing.</summary>
    Task CancelImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct);
}
