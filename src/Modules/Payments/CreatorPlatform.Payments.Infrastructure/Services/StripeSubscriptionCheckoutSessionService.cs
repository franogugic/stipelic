using CreatorPlatform.Payments.Application.Dtos;
using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Shared.Application.Exceptions;
using System.Net;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace CreatorPlatform.Payments.Infrastructure.Services;

public sealed class StripeSubscriptionCheckoutSessionService : ISubscriptionCheckoutSessionService
{
    private readonly StripeOptions _options;

    public StripeSubscriptionCheckoutSessionService(IOptions<StripeOptions> options)
    {
        _options = options.Value;
    }

    public async Task<SubscriptionCheckoutSessionDto> CreateAsync(
        string stripePriceId,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> metadata,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BadRequestException("Stripe secret key is not configured.");

        if (string.IsNullOrWhiteSpace(_options.SuccessUrl))
            throw new BadRequestException("Stripe success URL is not configured.");

        if (string.IsNullOrWhiteSpace(_options.CancelUrl))
            throw new BadRequestException("Stripe cancel URL is not configured.");

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = _options.SuccessUrl,
            CancelUrl = _options.CancelUrl,
            Metadata = new Dictionary<string, string>(metadata),
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>(metadata)
            },
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = stripePriceId,
                    Quantity = 1
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

        return new SubscriptionCheckoutSessionDto
        {
            ProviderCheckoutSessionId = session.Id,
            CheckoutUrl = session.Url
        };
    }

    public async Task<CheckoutSessionExpireOutcome> ExpireAsync(string checkoutSessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new InternalServerException("Stripe secret key is not configured.");

        var service = new SessionService();
        var requestOptions = new RequestOptions { ApiKey = _options.SecretKey };

        try
        {
            await service.ExpireAsync(checkoutSessionId, null, requestOptions, ct);
            return CheckoutSessionExpireOutcome.Expired;
        }
        catch (StripeException exception) when (exception.HttpStatusCode == HttpStatusCode.BadRequest)
        {
            // Stripe refuses to expire a session that is no longer open. Ask it which terminal state the
            // session is in instead of parsing the error message.
            Session session;
            try
            {
                session = await service.GetAsync(checkoutSessionId, null, requestOptions, ct);
            }
            catch (StripeException lookupException)
            {
                throw ProviderUnavailable(lookupException);
            }

            return session.Status switch
            {
                "complete" => CheckoutSessionExpireOutcome.AlreadyCompleted,
                "expired" => CheckoutSessionExpireOutcome.AlreadyExpired,
                _ => throw ProviderUnavailable(exception)
            };
        }
        catch (StripeException exception)
        {
            throw ProviderUnavailable(exception);
        }
    }

    private static InternalServerException ProviderUnavailable(StripeException exception) =>
        new("The payment provider could not be reached. Nothing was changed — please try again.", exception);
}
