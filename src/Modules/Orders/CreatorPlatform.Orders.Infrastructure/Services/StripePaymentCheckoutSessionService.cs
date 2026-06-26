using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace CreatorPlatform.Orders.Infrastructure.Services;

public sealed class StripePaymentCheckoutSessionService : IPaymentCheckoutSessionService
{
    private readonly StripeOptions _options;

    public StripePaymentCheckoutSessionService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
    }

    public async Task<PaymentCheckoutSessionDto> CreateAsync(
        string productName,
        int priceCents,
        string currency,
        string customerEmail,
        string successUrl,
        string cancelUrl,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = customerEmail,
            Metadata = new Dictionary<string, string>(metadata),
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = new Dictionary<string, string>(metadata)
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmount = priceCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = productName
                        }
                    }
                }
            ]
        };

        var service = new SessionService();
        var session = await service.CreateAsync(
            options,
            new RequestOptions
            {
                ApiKey = _options.SecretKey,
                IdempotencyKey = idempotencyKey
            },
            ct);

        return new PaymentCheckoutSessionDto(session.Id, session.Url);
    }
}
