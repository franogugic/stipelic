using System.Reflection;
using CreatorPlatform.Analytics.Infrastructure;
using CreatorPlatform.Auth.Infrastructure;
using CreatorPlatform.Creators.Infrastructure;
using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.LandingPages.Infrastructure;
using CreatorPlatform.Marketing.Infrastructure;
using CreatorPlatform.Media.Infrastructure;
using CreatorPlatform.Orders.Infrastructure;
using CreatorPlatform.Payments.Infrastructure;
using CreatorPlatform.Products.Infrastructure;

namespace CreatorPlatform.Worker;

/// <summary>
/// The modules whose EF configurations build the Worker's model. The FULL set, as in the API, not only the modules the
/// Worker uses: a partial model would map a referenced entity by convention (wrong table, no key config) instead of failing.
/// Access and Customers have no entities of their own. A new module with entities is added here AND in the API's list;
/// the model tests fail until both match the migrations.
/// </summary>
public static class WorkerEntityConfigurations
{
    public static IReadOnlyList<Assembly> Assemblies { get; } =
    [
        typeof(AnalyticsInfrastructureServiceCollectionExtensions).Assembly,
        typeof(AuthInfrastructureServiceCollectionExtensions).Assembly,
        typeof(CreatorsInfrastructureServiceCollectionExtensions).Assembly,
        typeof(EmailInfrastructureServiceCollectionExtensions).Assembly,
        typeof(LandingPagesInfrastructureServiceCollectionExtensions).Assembly,
        typeof(MarketingInfrastructureServiceCollectionExtensions).Assembly,
        typeof(MediaInfrastructureServiceCollectionExtensions).Assembly,
        typeof(OrdersInfrastructureServiceCollectionExtensions).Assembly,
        typeof(PaymentsInfrastructureServiceCollectionExtensions).Assembly,
        typeof(ProductsInfrastructureServiceCollectionExtensions).Assembly,
    ];
}
