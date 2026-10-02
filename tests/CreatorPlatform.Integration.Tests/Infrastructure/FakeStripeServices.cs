using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;

namespace CreatorPlatform.Integration.Tests.Infrastructure;

/// <summary>Stands in for Stripe Checkout: records calls and answers with a programmed outcome (or failure).</summary>
public sealed class FakeCheckoutSessionService : ISubscriptionCheckoutSessionService
{
    public CheckoutSessionExpireOutcome ExpireOutcome { get; set; } = CheckoutSessionExpireOutcome.Expired;

    /// <summary>When set, ExpireAsync throws it — models Stripe being unreachable.</summary>
    public Exception? ExpireFailure { get; set; }

    /// <summary>The session id CreateAsync hands out next.</summary>
    public string NextSessionId { get; set; } = "cs_test_default";

    public List<string> ExpireCalls { get; } = [];

    public Task<SubscriptionCheckoutSessionDto> CreateAsync(
        string stripePriceId, string idempotencyKey, IReadOnlyDictionary<string, string> metadata, CancellationToken ct)
    {
        return Task.FromResult(new SubscriptionCheckoutSessionDto
        {
            ProviderCheckoutSessionId = NextSessionId,
            CheckoutUrl = $"https://checkout.stripe.test/{NextSessionId}"
        });
    }

    public Task<CheckoutSessionExpireOutcome> ExpireAsync(string checkoutSessionId, CancellationToken ct)
    {
        ExpireCalls.Add(checkoutSessionId);

        if (ExpireFailure is not null)
            throw ExpireFailure;

        return Task.FromResult(ExpireOutcome);
    }
}

public sealed class UnexpectedSubscriptionCancellationService : ISubscriptionCancellationService
{
    public Task CancelAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct)
        => throw new InvalidOperationException("Not expected to be called in this scenario.");
}

public sealed class UnexpectedBillingPortalService : IBillingPortalService
{
    public Task<string> CreateSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct)
        => throw new InvalidOperationException("Not expected to be called in this scenario.");
}
