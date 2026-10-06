using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary><c>designer-prototype/emails/reset-password.html</c>.</summary>
public static class PasswordResetTemplate
{
    public const string Subject = "Reset your password";

    public static RenderedEmail Render(EmailBranding branding, string? firstName, string email, string resetUrl)
    {
        var card =
            Heading("Reset your ", "password.") +
            Greeting(firstName) +
            Paragraph($"We got a request to reset the password for {Strong(email)}. Choose a new one with the button below.") +
            Button("Choose a new password", resetUrl) +
            FallbackLink(resetUrl) +
            Note("The link expires in 1 hour and works once. If you didn’t ask for this, ignore this email — your password stays the same.");

        var html = Document(Subject, "Choose a new password for your Luma account.", LumaHeader(branding.LogoUrl), card, LumaFooter(branding));

        var text = $"""
            {EmailVerificationTemplate.PlainGreeting(firstName)}

            We got a request to reset the password for {email}. Choose a new one here:
            {resetUrl}

            The link expires in 1 hour and works once. If you didn’t ask for this, ignore this email — your password stays the same.

            — Luma
            """;

        return new RenderedEmail(Subject, html, text);
    }
}
