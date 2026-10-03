using System.Net;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary>Sent to the OLD address after a change: the account owner learns about it even if someone else made
/// it. The new address is shown masked, so this mail doesn't hand it to whoever reads the old inbox.</summary>
public static class EmailChangedTemplate
{
    public const string Subject = "Your email was changed";

    public static string BuildHtml(string maskedNewEmail)
    {
        var encoded = WebUtility.HtmlEncode(maskedNewEmail);
        return $"""
            <h1>Your email was changed</h1>
            <p>The email address you use to sign in to Luma was changed to <strong>{encoded}</strong>.
            You have been signed out on your other devices.</p>
            <p>If you didn't make this change, contact support right away.</p>
            """;
    }

    public static string BuildPlainText(string maskedNewEmail)
    {
        return $"""
            Your email was changed

            The email address you use to sign in to Luma was changed to {maskedNewEmail}.
            You have been signed out on your other devices.

            If you didn't make this change, contact support right away.
            """;
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
