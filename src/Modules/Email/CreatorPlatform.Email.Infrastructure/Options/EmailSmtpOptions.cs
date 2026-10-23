namespace CreatorPlatform.Email.Infrastructure.Options;

/// <summary><c>Email:Smtp</c>: the SMTP server used when <see cref="EmailOptions.Provider"/> is <see cref="EmailProvider.Smtp"/>.</summary>
public sealed class EmailSmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    /// <summary>STARTTLS. False for Mailpit, which listens in plain text on localhost.</summary>
    public bool UseTls { get; set; }

    /// <summary>Optional; set together with <see cref="Password"/>.</summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }
}
