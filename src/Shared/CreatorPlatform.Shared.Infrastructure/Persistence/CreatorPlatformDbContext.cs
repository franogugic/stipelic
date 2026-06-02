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
        
        // za sat ne triba jer u ovom proejktu nema konfig nikakve
        //modelBuilder.ApplyConfigurationsFromAssembly(typeof(CreatorPlatformDbContext).Assembly);

        foreach (var assembly in GetLoadedInfrastructureAssemblies())
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    // pokupi sve assyempye koji pocinju s creatorplatofr i zavrsavaju s .inf
    // da skupimo sve konfiguracije jer su raspojedljenje po modulesima
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
