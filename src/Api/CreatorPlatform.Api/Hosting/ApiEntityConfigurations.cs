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
using CreatorPlatform.Payouts.Infrastructure;
using CreatorPlatform.Products.Infrastructure;

namespace CreatorPlatform.Api.Hosting;

/// <summary>
/// The modules whose EF configurations build the API's model: every module with persisted entities, so the model is the
/// one the migrations describe (the migration step, <c>--migrate</c>, runs on it). Access and Customers have no
/// entities of their own. A new module with entities is added here AND in the Worker's list; the model tests fail
/// until both match the migrations.
/// </summary>
public static class ApiEntityConfigurations
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
        typeof(PayoutsInfrastructureServiceCollectionExtensions).Assembly,
        typeof(ProductsInfrastructureServiceCollectionExtensions).Assembly,
    ];
}
