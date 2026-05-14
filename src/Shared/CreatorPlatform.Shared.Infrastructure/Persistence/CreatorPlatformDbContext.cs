using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace CreatorPlatform.Shared.Infrastructure.Persistence;

public sealed class CreatorPlatformDbContext : DbContext
{
    public CreatorPlatformDbContext(DbContextOptions<CreatorPlatformDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CreatorPlatformDbContext).Assembly);

        foreach (var assembly in GetLoadedInfrastructureAssemblies())
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    private static IEnumerable<Assembly> GetLoadedInfrastructureAssemblies()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly =>
            {
                var assemblyName = assembly.GetName().Name;

                return assemblyName is not null
                    && assemblyName.StartsWith("CreatorPlatform.", StringComparison.Ordinal)
                    && assemblyName.EndsWith(".Infrastructure", StringComparison.Ordinal)
                    && assembly != typeof(CreatorPlatformDbContext).Assembly;
            });
    }
}
