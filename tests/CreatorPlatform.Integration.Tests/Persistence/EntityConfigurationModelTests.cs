using System.Reflection;
using CreatorPlatform.Api.Hosting;
using CreatorPlatform.Email.Domain.Outbox;
using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.Orders.Domain.Orders;
using CreatorPlatform.Orders.Infrastructure;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using CreatorPlatform.Worker;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Persistence;

/// <summary>U7: each host registers its configuration assemblies explicitly. Both hosts' lists must build exactly the
/// model the migrations describe; a context without a list, or with a module missing, must not pass unnoticed. No
/// database is needed: the model is compared with the migrations' snapshot.</summary>
public sealed class EntityConfigurationModelTests
{
    // Never opened: building the model and diffing it with the snapshot needs the provider, not a server.
    private const string UnusedConnectionString = "Host=localhost;Database=model_check;Username=x;Password=x";

    private static CreatorPlatformDbContext Context(IEnumerable<Assembly>? assemblies)
    {
        var builder = new DbContextOptionsBuilder<CreatorPlatformDbContext>().UseNpgsql(UnusedConnectionString);
        if (assemblies is not null)
            builder.UseEntityConfigurationsFrom(assemblies);
        return new CreatorPlatformDbContext(builder.Options);
    }

    [Fact]
    public void TheApiList_BuildsTheModelTheMigrationsDescribe()
    {
        using var context = Context(ApiEntityConfigurations.Assemblies);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void TheWorkerList_BuildsTheModelTheMigrationsDescribe()
    {
        using var context = Context(WorkerEntityConfigurations.Assemblies);

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void BothHosts_ListTheSameModules()
    {
        Assert.Equal(
            ApiEntityConfigurations.Assemblies.Select(assembly => assembly.GetName().Name).Order(),
            WorkerEntityConfigurations.Assemblies.Select(assembly => assembly.GetName().Name).Order());
    }

    [Fact]
    public void WithoutAList_TheContextRefusesToBuildAModel()
    {
        using var context = Context(assemblies: null);

        var error = Assert.Throws<InvalidOperationException>(() => context.Model);
        Assert.Contains(nameof(EntityConfigurationAssemblies.UseEntityConfigurationsFrom), error.Message);
    }

    [Fact]
    public void AListMissingAModule_DoesNotMatchTheMigrations()
    {
        var withoutOrders = ApiEntityConfigurations.Assemblies
            .Where(assembly => assembly != typeof(OrdersInfrastructureServiceCollectionExtensions).Assembly);
        using var context = Context(withoutOrders);

        Assert.True(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void DifferentLists_GetDifferentModels_InOneProcess()
    {
        using var full = Context(ApiEntityConfigurations.Assemblies);
        using var emailOnly = Context([typeof(EmailInfrastructureServiceCollectionExtensions).Assembly]);
        using var fullAgain = Context(ApiEntityConfigurations.Assemblies);

        Assert.NotNull(full.Model.FindEntityType(typeof(Order)));
        Assert.NotNull(emailOnly.Model.FindEntityType(typeof(EmailOutboxMessage)));
        Assert.Null(emailOnly.Model.FindEntityType(typeof(Order)));
        Assert.Same(full.Model, fullAgain.Model);   // same list: the cached model is reused
    }

    [Fact]
    public void TheListOrder_DoesNotMatter()
    {
        using var listed = Context(ApiEntityConfigurations.Assemblies);
        using var reversed = Context(ApiEntityConfigurations.Assemblies.Reverse());

        Assert.Same(listed.Model, reversed.Model);
    }
}
