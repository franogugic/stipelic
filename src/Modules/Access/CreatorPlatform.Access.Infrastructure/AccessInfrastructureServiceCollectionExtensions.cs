using CreatorPlatform.Access.Application.Interfaces;
using CreatorPlatform.Access.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Access.Infrastructure;

public static class AccessInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAccessInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAccessRedirectService, AccessRedirectService>();
        return services;
    }
}
