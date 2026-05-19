using CreatorPlatform.Auth.Application.Interfaces;
using CreatorPlatform.Auth.Application.Services;
using CreatorPlatform.Auth.Infrastructure.Persistence;
using CreatorPlatform.Auth.Infrastructure.Repositories;
using CreatorPlatform.Auth.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Auth.Infrastructure;

public static class AuthInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenGenerator, TokenGenerator>();
        services.AddScoped<ITokenHasher, Sha256TokenHasher>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        
        return services;
    }
}
