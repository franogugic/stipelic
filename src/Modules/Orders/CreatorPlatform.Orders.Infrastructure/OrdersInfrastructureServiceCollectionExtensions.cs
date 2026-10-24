using CreatorPlatform.Orders.Application.Interfaces;
using CreatorPlatform.Orders.Application.Services;
using CreatorPlatform.Orders.Infrastructure.Persistence;
using CreatorPlatform.Orders.Infrastructure.Repositories;
using CreatorPlatform.Orders.Infrastructure.Services;
using CreatorPlatform.Orders.Infrastructure.Workspaces;
using CreatorPlatform.Shared.Application.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Orders.Infrastructure;

public static class OrdersInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IHomeSummaryCache, HomeSummaryCache>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderReceiptReader, OrderReceiptReader>();
        services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
        services.AddScoped<IOrdersUnitOfWork, OrdersUnitOfWork>();
        services.AddScoped<IOrderAccessGrants, OrderAccessGrants>();
        services.AddScoped<IPaymentCheckoutSessionService, StripePaymentCheckoutSessionService>();
        services.AddScoped<IOrderCheckoutService, OrderCheckoutService>();
        services.AddScoped<IOrderWebhookService, OrderWebhookService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderReceiptService, OrderReceiptService>();

        services.AddScoped<IWorkspaceDataPurger, OrdersWorkspaceDataPurger>();

        return services;
    }
}
