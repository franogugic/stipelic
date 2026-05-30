using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Payments.Infrastructure;

public static class PaymentsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ISubscriptionCheckoutSessionService, StripeSubscriptionCheckoutSessionService>();
        services.AddScoped<IStripeWebhookService, StripeWebhookService>();

        return services;
    }
}
