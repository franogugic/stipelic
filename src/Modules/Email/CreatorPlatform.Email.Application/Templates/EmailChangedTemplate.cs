using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary><c>designer-prototype/emails/email-change-notice.html</c>. Sent to the OLD address after a change, so the
/// owner learns about it even if someone else made it. The new address is shown masked, so this mail doesn't hand it
/// to whoever reads the old inbox.</summary>
public static class EmailChangedTemplate
{
    public const string Subject = "Your email was changed";

    public static RenderedEmail Render(
        EmailBranding branding, string? firstName, string oldEmail, string maskedNewEmail, DateTimeOffset changedAt,
        string secureAccountUrl)
    {
        var help = string.IsNullOrWhiteSpace(branding.SupportEmail)
            ? "Secure your account now: reset your password."
            : $"Secure your account now: reset your password and write to {branding.SupportEmail} — we’ll help you get access back.";

        var card =
            Heading("Your email was ", "changed.") +
            Greeting(firstName) +
            Paragraph("The email on your Luma account was changed. From now on, log in with the new address.") +
            KeyValues(Row("Old email", oldEmail), Row("New email", maskedNewEmail), Row("Changed", DateTime(changedAt))) +
            Warning("Wasn’t you?", E(help)) +
            Button("Secure my account", secureAccountUrl);

        var html = Document(
            Subject,
            $"The email on your Luma account is now {maskedNewEmail}.",
            LumaHeader(branding.LogoUrl),
            card,
            LumaFooter(branding, "We send this notice to your old address for your security."));

        var text = $"""
            {EmailVerificationTemplate.PlainGreeting(firstName)}

            The email on your Luma account was changed. From now on, log in with the new address.

            Old email: {oldEmail}
            New email: {maskedNewEmail}
            Changed: {DateTime(changedAt)}

            Wasn’t you? {help}
            {secureAccountUrl}

            We send this notice to your old address for your security.
            — Luma
            """;

        return new RenderedEmail(Subject, html, text);
    }

    /// <summary>"ana.horvat@example.com" → "a*********@example.com"; keeps the first character and the domain.</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
            return "***";

        var local = email[..at];
        return local[0] + new string('*', Math.Max(local.Length - 1, 3)) + email[at..];
    }
}
