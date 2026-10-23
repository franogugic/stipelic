using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Infrastructure;
using CreatorPlatform.Email.Infrastructure.Options;
using CreatorPlatform.Email.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Azure.Communication.Email;

namespace CreatorPlatform.Integration.Tests.Email;

/// <summary>DEV-1: which sender a host gets, and the start-time guard that keeps SMTP out of real hosts.</summary>
public sealed class EmailProviderSelectionTests
{
    private const string AcsConnectionString = "endpoint=https://test.communication.azure.com/;accesskey=dGVzdA==";

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static (IServiceCollection Services, ServiceProvider Provider) Build(string environment, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddEmailInfrastructure(configuration, new TestEnvironment(environment));
        return (services, services.BuildServiceProvider());
    }

    private static Type? SenderType(IServiceCollection services) =>
        services.Single(d => d.ServiceType == typeof(IEmailSender)).ImplementationType;

    [Fact]
    public void Development_WithoutAProvider_SendsToTheLocalMailpit_AndNeedsNoAcsSettings()
    {
        var (services, provider) = Build(Environments.Development, []);

        var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
        Assert.Equal(EmailProvider.Smtp, options.Provider);
        Assert.Equal(("localhost", 1025, false), (options.Smtp.Host, options.Smtp.Port, options.Smtp.UseTls));
        Assert.Equal(EmailOptionsDefaults.DevelopmentFromAddress, options.FromAddress);
        Assert.Equal(typeof(SmtpEmailSender), SenderType(services));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(EmailClient));
    }

    [Fact]
    public void Development_SwitchesBackToRealSending_WithOneSetting()
    {
        var (services, provider) = Build(Environments.Development, new()
        {
            ["Email:Provider"] = "Acs",
            ["Email:ConnectionString"] = AcsConnectionString,
            ["Email:FromAddress"] = "DoNotReply@test.azurecomm.net",
        });

        Assert.Equal(EmailProvider.Acs, provider.GetRequiredService<IOptions<EmailOptions>>().Value.Provider);
        Assert.Equal(typeof(AzureEmailSender), SenderType(services));
        Assert.Contains(services, d => d.ServiceType == typeof(EmailClient));
    }

    [Fact]
    public void OutsideDevelopment_WithoutAProvider_SendsThroughAcs()
    {
        var (services, provider) = Build(Environments.Production, new()
        {
            ["Email:ConnectionString"] = AcsConnectionString,
            ["Email:FromAddress"] = "DoNotReply@test.azurecomm.net",
        });

        Assert.Equal(EmailProvider.Acs, provider.GetRequiredService<IOptions<EmailOptions>>().Value.Provider);
        Assert.Equal(typeof(AzureEmailSender), SenderType(services));
    }

    [Fact]
    public void OutsideDevelopment_Smtp_IsRefusedAtStart_UnlessExplicitlyAllowed()
    {
        Dictionary<string, string?> smtp = new()
        {
            ["Email:Provider"] = "Smtp",
            ["Email:FromAddress"] = "no-reply@luma.test",
            ["Email:Smtp:Host"] = "smtp.example.test",
            ["Email:Smtp:Port"] = "587",
        };

        var (_, refused) = Build(Environments.Production, smtp);
        var error = Assert.Throws<OptionsValidationException>(() => refused.GetRequiredService<IOptions<EmailOptions>>().Value);
        Assert.Contains("AllowSmtpOutsideDevelopment", error.Message);

        var (_, allowed) = Build(Environments.Staging, new(smtp) { ["Email:AllowSmtpOutsideDevelopment"] = "true" });
        Assert.Equal(EmailProvider.Smtp, allowed.GetRequiredService<IOptions<EmailOptions>>().Value.Provider);
    }

    [Theory]
    [InlineData("Production", "Email:ConnectionString")]                       // ACS without its connection string
    [InlineData("Development", "Email:Smtp:UserName and Email:Smtp:Password")]  // a user without a password
    public void AnIncompleteConfiguration_FailsAtStart(string environment, string expected)
    {
        var (_, provider) = Build(environment, new()
        {
            ["Email:FromAddress"] = "no-reply@luma.test",
            ["Email:Smtp:UserName"] = "user",
        });

        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<EmailOptions>>().Value);
        Assert.Contains(expected, error.Message);
    }
}
