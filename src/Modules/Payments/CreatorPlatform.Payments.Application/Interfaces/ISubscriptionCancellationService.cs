namespace CreatorPlatform.Payments.Application.Interfaces;

public interface ISubscriptionCancellationService
{
    Task CancelAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct);
}
