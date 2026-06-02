using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Payments.Application.Interfaces;

public interface IStripeWebhookService
{
    StripeWebhookEventDto ParseAndVerify(string payload, string stripeSignature);
}
