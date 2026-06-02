namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IBillingPortalService
{
    Task<string> CreateSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct);
}
