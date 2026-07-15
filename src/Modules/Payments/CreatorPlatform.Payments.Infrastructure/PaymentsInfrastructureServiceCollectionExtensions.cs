using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Payments.Infrastructure.Repositories;
using CreatorPlatform.Payments.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stripe;

namespace CreatorPlatform.Payments.Infrastructure;

public static class PaymentsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services)
    {
        StripeConfiguration.MaxNetworkRetries = 3;

        services.AddScoped<ISubscriptionCheckoutSessionService, StripeSubscriptionCheckoutSessionService>();
        services.AddScoped<ISubscriptionCancellationService, StripeSubscriptionCancellationService>();
        services.AddScoped<IBillingPortalService, StripeBillingPortalService>();
        services.AddScoped<IStripeWebhookService, StripeWebhookService>();
        services.AddScoped<IWebhookFailureRepository, WebhookFailureRepository>();

        // Single shared StripeClient for the newer, client-based Connect services (StripeConnectAccountService) —
        // existing per-call services (SessionService, etc.) are untouched and keep using their own RequestOptions.ApiKey.
        services.AddSingleton<StripeClient>(sp =>
            new StripeClient(sp.GetRequiredService<IOptions<StripeOptions>>().Value.SecretKey));
        services.AddScoped<IConnectAccountService, StripeConnectAccountService>();

        return services;
    }
}
