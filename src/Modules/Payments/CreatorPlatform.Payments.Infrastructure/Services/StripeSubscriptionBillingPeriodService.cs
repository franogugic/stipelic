using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;
using Stripe;

namespace CreatorPlatform.Payments.Infrastructure.Services;

public sealed class StripeSubscriptionBillingPeriodService : ISubscriptionBillingPeriodService
{
    private readonly StripeOptions _options;

    public StripeSubscriptionBillingPeriodService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
    }

    public async Task<SubscriptionBillingPeriodDto?> GetBillingPeriodAsync(string stripeSubscriptionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InternalServerException("Stripe secret key is not configured.");

        var subscription = await new SubscriptionService().GetAsync(
            stripeSubscriptionId,
            null,
            new RequestOptions { ApiKey = _options.SecretKey },
            ct);

        // API "basil" (Stripe.net 51) keeps current_period_start/end on the subscription items, not on the
        // subscription. Platform subscriptions have exactly one item (one plan price).
        var item = subscription.Items?.Data?.FirstOrDefault();
        if (item is null)
            return null;

        return new SubscriptionBillingPeriodDto(
            new DateTimeOffset(DateTime.SpecifyKind(item.CurrentPeriodStart, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(item.CurrentPeriodEnd, DateTimeKind.Utc)));
    }
}
