using CreatorPlatform.LandingPages.Application.Interfaces;
using CreatorPlatform.LandingPages.Application.Services;
using CreatorPlatform.LandingPages.Infrastructure.Persistence;
using CreatorPlatform.LandingPages.Infrastructure.Repositories;
using CreatorPlatform.LandingPages.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.LandingPages.Infrastructure;

public static class LandingPagesInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddLandingPagesInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ILandingPageService, LandingPageService>();
        services.AddScoped<ILandingPageRepository, LandingPageRepository>();
        services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
        services.AddScoped<ILandingPagesUnitOfWork, LandingPagesUnitOfWork>();

        return services;
    }
}
