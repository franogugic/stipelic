namespace CreatorPlatform.Email.Infrastructure.Options;

/// <summary>Where outgoing email goes (<c>Email:Provider</c>).</summary>
public enum EmailProvider
{
    /// <summary>Azure Communication Services: real delivery.</summary>
    Acs,

    /// <summary>An SMTP server — in Development the local Mailpit catcher (see docker-compose.yml).</summary>
    Smtp
}
