using CreatorPlatform.Payments.Application.Interfaces;
using CreatorPlatform.Payments.Application.Options;
using CreatorPlatform.Payments.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Stripe;

namespace CreatorPlatform.Payments.Infrastructure;

public static class PaymentsWorkerServiceCollectionExtensions
{
    /// <summary>For the Worker: the <c>Stripe</c> settings (the same section and values as the API's) and the two Stripe
    /// calls finishing a workspace deletion needs: expiring its sale Checkout sessions and deleting its connected
    /// account. Not the rest of Payments (webhooks, billing).</summary>
    public static IServiceCollection AddStripeForWorkspaceDeletion(this IServiceCollection services, IConfiguration configuration)
    {
        StripeConfiguration.MaxNetworkRetries = 3;

        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.TryAddSingleton<StripeClient>(sp =>
            new StripeClient(sp.GetRequiredService<IOptions<StripeOptions>>().Value.SecretKey));
        services.TryAddScoped<ISubscriptionCheckoutSessionService, StripeSubscriptionCheckoutSessionService>();
        services.TryAddScoped<IConnectAccountService, StripeConnectAccountService>();

        return services;
    }
}
