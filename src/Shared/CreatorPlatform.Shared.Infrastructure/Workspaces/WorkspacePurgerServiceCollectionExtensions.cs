using System.Reflection;
using CreatorPlatform.Shared.Application.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CreatorPlatform.Shared.Infrastructure.Workspaces;

public static class WorkspacePurgerServiceCollectionExtensions
{
    /// <summary>
    /// Registers every <see cref="IWorkspaceDataPurger"/> of the given module assemblies. The API and the Worker pass the
    /// assemblies their EF model is built from (every module with persisted entities), so a module's purger is found
    /// wherever its tables are, and both hosts erase the same tables.
    /// </summary>
    public static IServiceCollection AddWorkspaceDataPurgers(this IServiceCollection services, IEnumerable<Assembly> moduleAssemblies)
    {
        foreach (var purger in FindPurgers(moduleAssemblies))
            services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IWorkspaceDataPurger), purger));

        return services;
    }

    public static IReadOnlyList<Type> FindPurgers(IEnumerable<Assembly> moduleAssemblies) =>
        moduleAssemblies
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IWorkspaceDataPurger).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
}
