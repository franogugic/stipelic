using CreatorPlatform.Marketing.Application.Interfaces;
using CreatorPlatform.Marketing.Infrastructure.Persistence;
using CreatorPlatform.Marketing.Infrastructure.Repositories;
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

        return services;
    }
}
