using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.BillingPortal;

namespace CreatorPlatform.Payments.Infrastructure.Services;

public sealed class StripeBillingPortalService : IBillingPortalService
{
    private readonly StripeOptions _options;

    public StripeBillingPortalService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> CreateSessionAsync(
        string stripeCustomerId,
        string returnUrl,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        if (string.IsNullOrWhiteSpace(_options.BillingPortalReturnUrl))
            throw new BadRequestException("Billing portal return URL is not configured.");

        var fullReturnUrl = $"{_options.BillingPortalReturnUrl.TrimEnd('/')}{returnUrl}";

        var service = new SessionService();
        var session = await service.CreateAsync(
            new SessionCreateOptions
            {
                Customer = stripeCustomerId,
                ReturnUrl = fullReturnUrl,
            },
            new RequestOptions { ApiKey = _options.SecretKey },
            ct);

        return session.Url;
    }
}
