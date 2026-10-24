using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IStripeWebhookService
{
    StripeWebhookEventDto ParseAndVerify(string payload, string stripeSignature);

    /// <summary>Same shape, but verified against the separate Connect webhook signing secret —
    /// connected-account events (e.g. account.updated) are registered under their own endpoint in Stripe.</summary>
    StripeWebhookEventDto ParseAndVerifyConnect(string payload, string stripeSignature);

    /// <summary>Parses a payload that was verified when it arrived and stored since (a webhook failure being replayed) —
    /// no signature check, the stored row is the trust anchor. Throws <c>BadRequestException</c> when it is not an event.</summary>
    StripeWebhookEventDto Parse(string payload);
}
