using CreatorPlatform.Analytics.Application.Interfaces;
using CreatorPlatform.Analytics.Application.Services;
using CreatorPlatform.Analytics.Infrastructure.Persistence;
using CreatorPlatform.Analytics.Infrastructure.Repositories;
using CreatorPlatform.Analytics.Infrastructure.Services;
using CreatorPlatform.Analytics.Infrastructure.Workspaces;
using CreatorPlatform.Shared.Application.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Analytics.Infrastructure;

public static class AnalyticsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAnalyticsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPageViewService, PageViewService>();
        services.AddScoped<IPageViewRepository, PageViewRepository>();
        services.AddScoped<IEmailCaptureService, EmailCaptureService>();
        services.AddScoped<IEmailCaptureRepository, EmailCaptureRepository>();
        services.AddSingleton<IViewsSummaryCache, ViewsSummaryCache>();
        services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
        services.AddScoped<IAnalyticsUnitOfWork, AnalyticsUnitOfWork>();
        services.AddScoped<IAnalyticsSeriesReader, AnalyticsSeriesReader>();
        services.AddScoped<IAnalyticsSeriesService, AnalyticsSeriesService>();

        services.AddScoped<IWorkspaceDataPurger, AnalyticsWorkspaceDataPurger>();

        return services;
    }
}
