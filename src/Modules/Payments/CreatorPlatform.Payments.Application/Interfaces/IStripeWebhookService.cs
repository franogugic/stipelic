using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IStripeWebhookService
{
    StripeWebhookEventDto ParseAndVerify(string payload, string stripeSignature);

    /// <summary>Same shape, but verified against the separate Connect webhook signing secret —
    /// connected-account events (e.g. account.updated) are registered under their own endpoint in Stripe.</summary>
    StripeWebhookEventDto ParseAndVerifyConnect(string payload, string stripeSignature);
}
