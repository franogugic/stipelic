namespace CreatorPlatform.Payments.Application.Options;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; init; } = string.Empty;

    public string WebhookSecret { get; init; } = string.Empty;

    public string SuccessUrl { get; init; } = string.Empty;

    public string CancelUrl { get; init; } = string.Empty;

    public string BillingPortalReturnUrl { get; init; } = string.Empty;

    /// <summary>Frontend origin used to build Connect onboarding return/refresh URLs.</summary>
    public string FrontendBaseUrl { get; init; } = string.Empty;

    /// <summary>Signing secret for the separate Connect webhook endpoint registration (different from <see cref="WebhookSecret"/>).</summary>
    public string ConnectWebhookSecret { get; init; } = string.Empty;
}
