namespace CreatorPlatform.Email.Infrastructure.Options;

/// <summary>
/// The provider and SMTP settings a host gets when it leaves them unset. appsettings.Development.json is not in the
/// repository (it holds secrets), so the local defaults live here — the same pattern as the Redis Development defaults.
/// </summary>
public static class EmailOptionsDefaults
{
    /// <summary>The local Mailpit (docker-compose.yml): SMTP on host port 1025, web UI on http://localhost:8025.</summary>
    public const string DevelopmentSmtpHost = "localhost";

    public const int DevelopmentSmtpPort = 1025;

    /// <summary>The sender of caught mail when Development configures none (Mailpit accepts any address).</summary>
    public const string DevelopmentFromAddress = "no-reply@luma.test";

    /// <summary>Unset: SMTP (Mailpit) in Development, ACS everywhere else.</summary>
    public static EmailProvider ResolveProvider(EmailProvider? configured, bool isDevelopment) =>
        configured ?? (isDevelopment ? EmailProvider.Smtp : EmailProvider.Acs);

    public static void Apply(EmailOptions options, bool isDevelopment)
    {
        options.Provider = ResolveProvider(options.Provider, isDevelopment);

        if (!isDevelopment || options.Provider != EmailProvider.Smtp)
            return;

        if (string.IsNullOrWhiteSpace(options.Smtp.Host))
            options.Smtp.Host = DevelopmentSmtpHost;
        if (options.Smtp.Port == 0)
            options.Smtp.Port = DevelopmentSmtpPort;
        if (string.IsNullOrWhiteSpace(options.FromAddress))
            options.FromAddress = DevelopmentFromAddress;
    }
}
