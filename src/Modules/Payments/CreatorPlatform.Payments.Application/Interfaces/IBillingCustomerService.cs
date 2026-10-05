namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IBillingCustomerService
{
    /// <summary>Creates the Stripe customer a workspace is billed as and returns its id. The idempotency key makes a
    /// retry return the same customer instead of creating a second one.</summary>
    Task<string> CreateAsync(
        string email, string name, IReadOnlyDictionary<string, string> metadata, string idempotencyKey, CancellationToken ct);
}
