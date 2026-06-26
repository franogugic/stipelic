using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Persistence;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Orders.Infrastructure;

public static class OrdersInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
        services.AddScoped<IOrdersUnitOfWork, OrdersUnitOfWork>();
        services.AddScoped<IPaymentCheckoutSessionService, StripePaymentCheckoutSessionService>();
        services.AddScoped<IOrderCheckoutService, OrderCheckoutService>();
        services.AddScoped<IOrderWebhookService, OrderWebhookService>();

        return services;
    }
}
