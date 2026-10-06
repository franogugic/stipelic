using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary><c>designer-prototype/emails/email-change-confirm.html</c>. Sent to the NEW address: confirming proves the
/// user controls it.</summary>
public static class EmailChangeVerificationTemplate
{
    public const string Subject = "Confirm your new email";

    public static RenderedEmail Render(
        EmailBranding branding, string? firstName, string currentEmail, string newEmail, string confirmationUrl)
    {
        var card =
            Heading("Confirm your ", "new email.") +
            Greeting(firstName) +
            Paragraph($"You asked to change the email on your Luma account from {Strong(currentEmail)} to {Strong(newEmail)}. Confirm to finish — until then, nothing changes.") +
            Button("Confirm new email", confirmationUrl) +
            FallbackLink(confirmationUrl) +
            Note("The link works for 24 hours. Didn’t ask for this? Ignore this email and your account stays as it is.");

        var html = Document(
            Subject, $"Confirm {newEmail} to finish changing your Luma email.", LumaHeader(branding.LogoUrl), card, LumaFooter(branding));

        var text = $"""
            {EmailVerificationTemplate.PlainGreeting(firstName)}

            You asked to change the email on your Luma account from {currentEmail} to {newEmail}. Confirm to finish — until then, nothing changes.

            Confirm your new email:
            {confirmationUrl}

            The link works for 24 hours. Didn’t ask for this? Ignore this email and your account stays as it is.

            — Luma
            """;

        return new RenderedEmail(Subject, html, text);
    }
}
