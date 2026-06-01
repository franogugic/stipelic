using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Infrastructure.Repositories;
using CreatorPlatform.Payments.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace CreatorPlatform.Payments.Infrastructure;

public static class PaymentsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services)
    {
        StripeConfiguration.MaxNetworkRetries = 3;

        services.AddScoped<ISubscriptionCheckoutSessionService, StripeSubscriptionCheckoutSessionService>();
        services.AddScoped<ISubscriptionCancellationService, StripeSubscriptionCancellationService>();
        services.AddScoped<IStripeWebhookService, StripeWebhookService>();
        services.AddScoped<IWebhookFailureRepository, WebhookFailureRepository>();

        return services;
    }
}
