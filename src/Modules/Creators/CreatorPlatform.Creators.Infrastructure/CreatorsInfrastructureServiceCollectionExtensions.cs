using CreatorPlatform.Creators.Application.Interfaces;
using CreatorPlatform.Creators.Application.Services;
using CreatorPlatform.Creators.Infrastructure.Persistence;
using CreatorPlatform.Creators.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Creators.Infrastructure;

public static class CreatorsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCreatorsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICreatorService, CreatorService>();
        services.AddScoped<ICreatorRepository, CreatorRepository>();
        services.AddScoped<ICreatorMemberRepository, CreatorMemberRepository>();
        services.AddScoped<ICreatorPlanRepository, CreatorPlanRepository>();
        services.AddScoped<ICreatorSettingsRepository, CreatorSettingsRepository>();
        services.AddScoped<ICreatorSubscriptionRepository, CreatorSubscriptionRepository>();
        services.AddScoped<ICreatorsUnitOfWork, CreatorsUnitOfWork>();

        return services;
    }
}
