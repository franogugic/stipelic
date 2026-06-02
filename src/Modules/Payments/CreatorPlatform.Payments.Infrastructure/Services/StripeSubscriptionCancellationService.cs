using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;
using Stripe;

namespace CreatorPlatform.Payments.Infrastructure.Services;

public sealed class StripeSubscriptionCancellationService : ISubscriptionCancellationService
{
    private readonly StripeOptions _options;

    public StripeSubscriptionCancellationService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
    }

    public async Task CancelAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        var service = new SubscriptionService();
        await service.UpdateAsync(
            stripeSubscriptionId,
            new SubscriptionUpdateOptions { CancelAtPeriodEnd = true },
            new RequestOptions { ApiKey = _options.SecretKey },
            ct);
    }
}
