using CreatorPlatform.Media.Application.Interfaces;
using CreatorPlatform.Media.Application.Options;
using CreatorPlatform.Media.Infrastructure.Services;
using CreatorPlatform.Media.Infrastructure.Workspaces;
using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Media.Infrastructure.Maintenance;

public static class MediaMaintenanceServiceCollectionExtensions
{
    /// <summary>For the Worker: the <c>Media</c> settings (the same section and values as the API's), the two storage
    /// clients, the abandoned-upload cleanup and a deleted workspace's files. Without <c>Media:ConnectionString</c> the
    /// cleanup logs once and skips, and deleted workspaces keep their files until it is set.</summary>
    public static IServiceCollection AddMediaMaintenanceTasks(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaOptions>()
            .Bind(configuration.GetSection(MediaOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MediaOptions>, MediaOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MaintenanceOptions>, AbandonedUploadsOptionsValidator>());

        services.TryAddScoped<IBlobStorageClient, AzureBlobStorageClient>();
        services.TryAddScoped<IProductFileStorage, AzureProductFileStorage>();
        services.TryAddScoped<IWorkspaceFiles, AzureWorkspaceFiles>();
        services.AddScoped<IMaintenanceTask, DeleteAbandonedUploadsTask>();

        return services;
    }
}
