namespace CreatorPlatform.Marketing.Domain.Mail;

/// <summary>Shared subject/body/CTA validation for anything that renders into an email — both
/// <see cref="Campaigns.Campaign"/> (a send's content snapshot) and
/// <see cref="Templates.EmailTemplate"/> (the reusable source of that content) enforce the exact same
/// rules through this one place, so the two can never drift.</summary>
public static class MailContentRules
{
    public const int MaxSubjectLength = 200;
    public const int MaxBodyTextLength = 10_000;
    public const int MaxCtaLabelLength = 100;
    public const int MaxCtaUrlLength = 2000;

    public static void Validate(string subject, string bodyText, string? ctaLabel, string? ctaUrl)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (subject.Trim().Length > MaxSubjectLength)
            throw new ArgumentException($"Subject must be at most {MaxSubjectLength} characters.", nameof(subject));

        if (string.IsNullOrWhiteSpace(bodyText))
            throw new ArgumentException("Body text is required.", nameof(bodyText));
        if (bodyText.Length > MaxBodyTextLength)
            throw new ArgumentException($"Body text must be at most {MaxBodyTextLength} characters.", nameof(bodyText));

        var hasCtaLabel = !string.IsNullOrWhiteSpace(ctaLabel);
        var hasCtaUrl = !string.IsNullOrWhiteSpace(ctaUrl);
        if (hasCtaLabel != hasCtaUrl)
            throw new ArgumentException("CTA label and URL must both be set, or both be empty.");
        if (hasCtaLabel && ctaLabel!.Trim().Length > MaxCtaLabelLength)
            throw new ArgumentException($"CTA label must be at most {MaxCtaLabelLength} characters.", nameof(ctaLabel));
        if (hasCtaUrl)
        {
            var trimmedCtaUrl = ctaUrl!.Trim();
            if (trimmedCtaUrl.Length > MaxCtaUrlLength)
                throw new ArgumentException($"CTA URL must be at most {MaxCtaUrlLength} characters.", nameof(ctaUrl));

            // Must be an absolute http/https URL — a "javascript:" or relative URL is inert in real mail
            // clients, but the web preview renders CtaUrl straight into an href, so it must never carry an
            // unvalidated scheme.
            var isAbsoluteHttpUrl =
                Uri.TryCreate(trimmedCtaUrl, UriKind.Absolute, out var parsedCtaUrl) &&
                (parsedCtaUrl.Scheme == Uri.UriSchemeHttp || parsedCtaUrl.Scheme == Uri.UriSchemeHttps);
            if (!isAbsoluteHttpUrl)
                throw new ArgumentException("CTA URL must be an absolute http:// or https:// URL.", nameof(ctaUrl));
        }
    }
}
