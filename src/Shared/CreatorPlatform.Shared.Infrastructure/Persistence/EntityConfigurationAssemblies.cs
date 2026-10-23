using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Shared.Infrastructure.Persistence;

public static class EntityConfigurationAssemblies
{
    /// <summary>
    /// The module assemblies whose <see cref="IEntityTypeConfiguration{TEntity}"/> classes build
    /// <see cref="CreatorPlatformDbContext"/>'s model. Every host lists them explicitly (the API's and the Worker's lists
    /// are the full set the migrations describe); nothing depends on which assemblies happen to be loaded.
    /// </summary>
    public static DbContextOptionsBuilder UseEntityConfigurationsFrom(
        this DbContextOptionsBuilder builder,
        IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assemblies);

        var extension = new EntityConfigurationAssembliesExtension(assemblies);
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(extension);
        return builder;
    }

    /// <inheritdoc cref="UseEntityConfigurationsFrom(DbContextOptionsBuilder, IEnumerable{Assembly})"/>
    public static DbContextOptionsBuilder<TContext> UseEntityConfigurationsFrom<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        IEnumerable<Assembly> assemblies)
        where TContext : DbContext =>
        (DbContextOptionsBuilder<TContext>)((DbContextOptionsBuilder)builder).UseEntityConfigurationsFrom(assemblies);
}

/// <summary>Carries the configuration assemblies on the context's options. Two option sets with different assemblies get
/// different EF internal service providers, and therefore different cached models.</summary>
public sealed class EntityConfigurationAssembliesExtension : IDbContextOptionsExtension
{
    private ExtensionInfo? _info;

    public EntityConfigurationAssembliesExtension(IEnumerable<Assembly> assemblies)
    {
        // Distinct and ordered by name: the model is built in the same order whatever order a host lists them in.
        Assemblies = assemblies
            .Distinct()
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .ToArray();

        if (Assemblies.Count == 0)
            throw new ArgumentException("At least one entity configuration assembly is required.", nameof(assemblies));
    }

    public IReadOnlyList<Assembly> Assemblies { get; }

    public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

    public void ApplyServices(IServiceCollection services)
    {
    }

    public void Validate(IDbContextOptions options)
    {
    }

    private sealed class ExtensionInfo(EntityConfigurationAssembliesExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        private new EntityConfigurationAssembliesExtension Extension => (EntityConfigurationAssembliesExtension)base.Extension;

        public override bool IsDatabaseProvider => false;

        public override string LogFragment =>
            $"EntityConfigurations={string.Join(",", Extension.Assemblies.Select(assembly => assembly.GetName().Name))} ";

        public override int GetServiceProviderHashCode()
        {
            var hash = new HashCode();
            foreach (var assembly in Extension.Assemblies)
                hash.Add(assembly);
            return hash.ToHashCode();
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) =>
            other is ExtensionInfo otherInfo && Extension.Assemblies.SequenceEqual(otherInfo.Extension.Assemblies);

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) =>
            debugInfo["CreatorPlatform:EntityConfigurations"] = GetServiceProviderHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
