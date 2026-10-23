using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Shared.Infrastructure.Maintenance;

public static class MaintenanceServiceCollectionExtensions
{
    /// <summary>The <c>Maintenance</c> section (validated at start) and the runner. The tasks come from the modules
    /// (<c>Add*MaintenanceTasks</c>).</summary>
    public static IServiceCollection AddMaintenance(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MaintenanceOptions>()
            .Bind(configuration.GetSection(MaintenanceOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MaintenanceOptions>, MaintenanceOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<MaintenanceRunner>();

        return services;
    }
}
