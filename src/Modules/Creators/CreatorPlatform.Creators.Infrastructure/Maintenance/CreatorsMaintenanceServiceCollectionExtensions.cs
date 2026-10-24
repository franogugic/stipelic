using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Infrastructure.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Maintenance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CreatorPlatform.Creators.Infrastructure.Maintenance;

public static class CreatorsMaintenanceServiceCollectionExtensions
{
    /// <summary>For the Worker: finishing deleted workspaces. Needs the module purgers
    /// (<c>AddWorkspaceDataPurgers</c>), the workspace files (Media) and Stripe (<c>AddStripeForWorkspaceDeletion</c>).</summary>
    public static IServiceCollection AddCreatorsMaintenanceTasks(this IServiceCollection services)
    {
        services.TryAddScoped<IWorkspaceErasure, WorkspaceErasure>();
        services.AddScoped<IMaintenanceTask, FinishDeletedWorkspacesTask>();

        return services;
    }
}
