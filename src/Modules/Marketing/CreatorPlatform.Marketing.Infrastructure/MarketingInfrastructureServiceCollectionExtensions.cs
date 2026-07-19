using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Application.Services;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
using CreatorPlatform.Marketing.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Marketing.Infrastructure;

public static class MarketingInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddMarketingInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<ICampaignRecipientRepository, CampaignRecipientRepository>();
        services.AddScoped<IUnsubscribeRepository, UnsubscribeRepository>();
        services.AddScoped<IMarketingUnitOfWork, MarketingUnitOfWork>();
        services.AddSingleton<IUnsubscribeTokenService, UnsubscribeTokenService>();
        services.AddScoped<ICreatorContextProvider, CreatorContextProvider>();
        services.AddScoped<IAudienceService, AudienceService>();
        services.AddScoped<ICampaignService, CampaignService>();

        return services;
    }
}
