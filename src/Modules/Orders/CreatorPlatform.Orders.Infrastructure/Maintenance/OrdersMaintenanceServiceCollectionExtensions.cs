using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Orders.Infrastructure.Maintenance;

public static class OrdersMaintenanceServiceCollectionExtensions
{
    public static IServiceCollection AddOrdersMaintenanceTasks(this IServiceCollection services)
    {
        services.AddScoped<IMaintenanceTask, FailStalePendingOrdersTask>();
        return services;
    }
}
