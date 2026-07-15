using CreatorPlatform.Payouts.Application.Interfaces;
using CreatorPlatform.Payouts.Application.Services;
using CreatorPlatform.Payouts.Infrastructure.Persistence;
using CreatorPlatform.Payouts.Infrastructure.Repositories;
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

        return services;
    }
}
