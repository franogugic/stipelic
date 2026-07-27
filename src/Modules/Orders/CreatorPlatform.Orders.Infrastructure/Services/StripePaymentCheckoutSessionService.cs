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
        CancellationToken ct,
        int? applicationFeeAmountCents = null,
        string? destinationAccountId = null,
        string? thumbnailUrl = null)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        var paymentIntentData = new SessionPaymentIntentDataOptions
        {
            Metadata = new Dictionary<string, string>(metadata)
        };

        if (applicationFeeAmountCents is not null && destinationAccountId is not null)
        {
            paymentIntentData.ApplicationFeeAmount = applicationFeeAmountCents;
            paymentIntentData.TransferData = new SessionPaymentIntentDataTransferDataOptions
            {
                Destination = destinationAccountId
            };
        }

        var productData = new SessionLineItemPriceDataProductDataOptions
        {
            Name = productName
        };

        // Stripe rejects an empty/null entry inside Images — only attach the list when there's a real URL.
        if (!string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            productData.Images = [thumbnailUrl];
        }

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            CustomerEmail = customerEmail,
            Metadata = new Dictionary<string, string>(metadata),
            PaymentIntentData = paymentIntentData,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmount = priceCents,
                        ProductData = productData
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
