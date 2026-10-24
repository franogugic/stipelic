using Azure.Communication.Email;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using CreatorPlatform.Email.Infrastructure.Workspaces;
using CreatorPlatform.Shared.Application.Workspaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure;

public static class EmailInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Binds and validates the <c>Email</c> section (the start fails on an invalid one) and registers the sender of the
    /// configured provider: <see cref="EmailProvider.Smtp"/> (Development default: the local Mailpit) creates no ACS
    /// client and needs no ACS connection string; <see cref="EmailProvider.Acs"/> sends through Azure Communication
    /// Services.
    /// </summary>
    public static IServiceCollection AddEmailInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(EmailOptions.SectionName);
        var isDevelopment = environment.IsDevelopment();

        services
            .AddOptions<EmailOptions>()
            .Bind(section)
            .PostConfigure(options => EmailOptionsDefaults.Apply(options, isDevelopment))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EmailOptions>>(new EmailOptionsValidator(isDevelopment)));

        var provider = EmailOptionsDefaults.ResolveProvider(
            section.GetValue<EmailProvider?>(nameof(EmailOptions.Provider)), isDevelopment);

        if (provider == EmailProvider.Smtp)
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            // Few SDK retries: the outbox has its own retries and pauses all sending on a 429, and each send has a
            // timeout (Email:Outbox:SendTimeoutSeconds) that long SDK retry waits would only run into.
            services.AddSingleton(serviceProvider => new EmailClient(
                serviceProvider.GetRequiredService<IOptions<EmailOptions>>().Value.ConnectionString,
                new EmailClientOptions { Retry = { MaxRetries = 2, NetworkTimeout = TimeSpan.FromSeconds(20) } }));
            services.AddScoped<IEmailSender, AzureEmailSender>();
        }

        services.AddScoped<IEmailOutboxService, EmailOutboxService>();

        services.AddScoped<IWorkspaceDataPurger, EmailWorkspaceDataPurger>();

        return services;
    }
}
