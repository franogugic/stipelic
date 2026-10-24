using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Shared.Infrastructure.Persistence;

public sealed class CreatorPlatformDbContext : DbContext
{
    private readonly DbContextOptions<CreatorPlatformDbContext> _options;

    public CreatorPlatformDbContext(DbContextOptions<CreatorPlatformDbContext> options)
        : base(options)
    {
        _options = options;
    }

    /// <summary>
    /// The model is built from the configuration assemblies the host registered with
    /// <see cref="EntityConfigurationAssemblies.UseEntityConfigurationsFrom"/> (each module keeps its own configurations).
    /// Without them the context refuses to build a model: a host with no or partial mappings would otherwise run against
    /// tables it maps wrongly or not at all.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var configurations = _options.FindExtension<EntityConfigurationAssembliesExtension>()
            ?? throw new InvalidOperationException(
                $"{nameof(CreatorPlatformDbContext)} has no entity configuration assemblies. Register them with " +
                $"options.{nameof(EntityConfigurationAssemblies.UseEntityConfigurationsFrom)}(...) where the context is added.");

        foreach (var assembly in configurations.Assemblies)
            modelBuilder.ApplyConfigurationsFromAssembly(assembly);
    }
}
