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

/// <summary>Stands in for reading a Stripe subscription's billing period: a programmed period, or a failure.</summary>
public sealed class FakeBillingPeriodService : ISubscriptionBillingPeriodService
{
    public SubscriptionBillingPeriodDto? Period { get; set; }

    /// <summary>When set, the read throws it — models Stripe being unreachable.</summary>
    public Exception? Failure { get; set; }

    public List<string> Reads { get; } = [];

    public Task<SubscriptionBillingPeriodDto?> GetBillingPeriodAsync(string stripeSubscriptionId, CancellationToken ct)
    {
        Reads.Add(stripeSubscriptionId);

        if (Failure is not null)
            throw Failure;

        return Task.FromResult(Period);
    }
}

public sealed class UnexpectedSubscriptionCancellationService : ISubscriptionCancellationService
{
    public Task CancelAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct)
        => throw new InvalidOperationException("Not expected to be called in this scenario.");

    public Task CancelImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct)
        => throw new InvalidOperationException("Not expected to be called in this scenario.");
}

/// <summary>Stands in for cancelling a Stripe subscription immediately: records calls, or fails.</summary>
public sealed class FakeSubscriptionCancellationService : ISubscriptionCancellationService
{
    /// <summary>When set, CancelImmediatelyAsync throws it — models Stripe being unreachable.</summary>
    public Exception? CancelFailure { get; set; }

    public List<string> CancelImmediatelyCalls { get; } = [];

    public Task CancelAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct)
        => throw new InvalidOperationException("Not expected to be called in this scenario.");

    public Task CancelImmediatelyAsync(string stripeSubscriptionId, CancellationToken ct)
    {
        CancelImmediatelyCalls.Add(stripeSubscriptionId);

        if (CancelFailure is not null)
            throw CancelFailure;

        return Task.CompletedTask;
    }
}

public sealed class UnexpectedBillingPortalService : IBillingPortalService
{
    public Task<string> CreateSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct)
        => throw new InvalidOperationException("Not expected to be called in this scenario.");
}

/// <summary>Records which workspaces' caches a service asked to drop.</summary>
public sealed class RecordingCreatorCacheInvalidator : CreatorPlatform.Creators.Application.Interfaces.ICreatorCacheInvalidator
{
    public List<int> Invalidated { get; } = [];

    public void Invalidate(int creatorId) => Invalidated.Add(creatorId);
}
