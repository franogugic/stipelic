using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Payouts.Infrastructure.Persistence;
using CreatorPlatform.Payouts.Infrastructure.Repositories;
using CreatorPlatform.Payouts.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Payouts.Infrastructure;

public static class PayoutsInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPayoutsInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<IPayoutsUnitOfWork, PayoutsUnitOfWork>();
        services.AddScoped<IPayoutLedgerService, PayoutLedgerService>();
        services.AddScoped<ICreatorPayoutContextProvider, CreatorPayoutContextProvider>();
        services.AddScoped<IPayoutAdminService, PayoutAdminService>();
        services.AddScoped<ICreatorPayoutService, CreatorPayoutService>();

        return services;
    }
}
