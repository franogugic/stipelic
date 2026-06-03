using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Analytics.Infrastructure;

public static class AnalyticsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAnalyticsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPageViewService, PageViewService>();
        services.AddScoped<IPageViewRepository, PageViewRepository>();

        return services;
    }
}
