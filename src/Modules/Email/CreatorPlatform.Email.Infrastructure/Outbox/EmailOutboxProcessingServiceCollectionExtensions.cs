using CreatorPlatform.Email.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure.Outbox;

public static class EmailOutboxProcessingServiceCollectionExtensions
{
    /// <summary>For the Worker, after <c>AddEmailInfrastructure</c>: the <c>Email:Outbox</c> settings (quota defaults by
    /// provider, validated at start), the throttle, the dispatcher and the hosted loop.</summary>
    public static IServiceCollection AddEmailOutboxProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOutboxOptions>()
            .Bind(configuration.GetSection(EmailOutboxOptions.SectionName))
            .PostConfigure<IOptions<EmailOptions>>((outbox, email) => EmailOutboxOptionsDefaults.Apply(outbox, email.Value.Provider))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<EmailOutboxOptions>, EmailOutboxOptionsValidator>());

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<EmailSendThrottle>();
        services.TryAddSingleton<EmailOutboxDispatcher>();
        services.AddHostedService<EmailOutboxProcessor>();

        return services;
    }
}
