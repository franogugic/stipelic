using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Auth.Infrastructure.Maintenance;

public static class AuthMaintenanceServiceCollectionExtensions
{
    public static IServiceCollection AddAuthMaintenanceTasks(this IServiceCollection services)
    {
        services.AddScoped<IMaintenanceTask, DeleteExpiredAuthRecordsTask>();
        return services;
    }
}
