using Azure.Communication.Email;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CreatorPlatform.Email.Infrastructure;

public static class EmailInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddEmailInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        services.AddSingleton(_ =>
        {
            var options = configuration
                .GetSection(EmailOptions.SectionName)
                .Get<EmailOptions>();

            if (string.IsNullOrWhiteSpace(options?.ConnectionString))
                throw new InvalidOperationException("Email connection string is not configured.");

            return new EmailClient(options.ConnectionString);
        });

        services.AddScoped<IEmailSender, AzureEmailSender>();
        services.AddScoped<IEmailOutboxService, EmailOutboxService>();

        return services;
    }
}
