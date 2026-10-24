using CreatorPlatform.Email.Infrastructure.Options;

namespace CreatorPlatform.Email.Infrastructure.Outbox;

/// <summary>
/// <c>Email:Outbox</c>: how the Worker sends the outbox. The quotas are <b>per Worker instance</b>: with N replicas, set
/// each to the provider's quota divided by N.
/// </summary>
public sealed class EmailOutboxOptions
{
    public const string SectionName = "Email:Outbox";

    /// <summary>Messages claimed per pass (fewer when the quota leaves less).</summary>
    public int BatchSize { get; set; } = 40;

    /// <summary>Sends in flight at once.</summary>
    public int MaxParallelSends { get; set; } = 8;

    /// <summary>The wait when nothing is ready. While messages are ready, passes follow each other without a pause.</summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>How long a claimed message is reserved for the instance that claimed it; after that, another instance may
    /// claim it again. Must cover a whole batch (checked at start).</summary>
    public int ProcessingLeaseMinutes { get; set; } = 5;

    /// <summary>One send call's limit; a send that takes longer counts as a failed attempt.</summary>
    public int SendTimeoutSeconds { get; set; } = 30;

    /// <summary>The provider's per-minute quota for this instance. Unset: ACS's default for a custom domain (30), or 600
    /// for SMTP (the local Mailpit).</summary>
    public int? MaxSendsPerMinute { get; set; }

    /// <summary>The provider's per-hour quota for this instance. Unset: ACS's default for a custom domain (100), or 36000
    /// for SMTP.</summary>
    public int? MaxSendsPerHour { get; set; }

    /// <summary>The share of each quota window only transactional mail may use, so order and password emails always find
    /// room, however big the campaigns waiting.</summary>
    public int TransactionalReservePercent { get; set; } = 20;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public TimeSpan ProcessingLease => TimeSpan.FromMinutes(ProcessingLeaseMinutes);

    public TimeSpan SendTimeout => TimeSpan.FromSeconds(SendTimeoutSeconds);

    public int PerMinute => MaxSendsPerMinute ?? 0;

    public int PerHour => MaxSendsPerHour ?? 0;
}

public static class EmailOutboxOptionsDefaults
{
    /// <summary>Azure Communication Services, custom domain, before a quota increase.</summary>
    public const int AcsPerMinute = 30;
    public const int AcsPerHour = 100;

    /// <summary>The local Mailpit has no quota; these only keep a runaway loop in check.</summary>
    public const int SmtpPerMinute = 600;
    public const int SmtpPerHour = 36_000;

    public static void Apply(EmailOutboxOptions options, EmailProvider? provider)
    {
        var smtp = provider == EmailProvider.Smtp;
        options.MaxSendsPerMinute ??= smtp ? SmtpPerMinute : AcsPerMinute;
        options.MaxSendsPerHour ??= smtp ? SmtpPerHour : AcsPerHour;
    }
}
