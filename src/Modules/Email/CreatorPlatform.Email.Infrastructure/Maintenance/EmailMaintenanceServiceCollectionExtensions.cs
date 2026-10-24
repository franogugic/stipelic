using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Email.Infrastructure.Maintenance;

public static class EmailMaintenanceServiceCollectionExtensions
{
    public static IServiceCollection AddEmailMaintenanceTasks(this IServiceCollection services)
    {
        services.AddScoped<IMaintenanceTask, PurgeSentEmailBodiesTask>();
        return services;
    }
}
