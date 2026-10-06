using static CreatorPlatform.Email.Application.Templates.EmailLayout;

namespace CreatorPlatform.Email.Application.Templates;

/// <summary><c>designer-prototype/emails/verify-email.html</c>.</summary>
public static class EmailVerificationTemplate
{
    public const string Subject = "Confirm your email";

    public static RenderedEmail Render(EmailBranding branding, string? firstName, string email, string verificationUrl)
    {
        var card =
            Heading("Confirm your ", "email.") +
            Greeting(firstName) +
            Paragraph($"Thanks for signing up. Confirm {Strong(email)} and your account is ready — next you’ll set up your workspace.") +
            Button("Confirm email", verificationUrl) +
            FallbackLink(verificationUrl) +
            Note("The link works for 24 hours. Didn’t sign up? You can safely ignore this email.");

        var html = Document(Subject, "One click and your Luma account is ready.", LumaHeader(branding.LogoUrl), card, LumaFooter(branding));

        var text = $"""
            {PlainGreeting(firstName)}

            Thanks for signing up. Confirm {email} and your account is ready — next you’ll set up your workspace.

            Confirm your email:
            {verificationUrl}

            The link works for 24 hours. Didn’t sign up? You can safely ignore this email.

            — Luma
            """;

        return new RenderedEmail(Subject, html, text);
    }

    internal static string PlainGreeting(string? firstName) =>
        string.IsNullOrWhiteSpace(firstName) ? "Hi," : $"Hi {firstName.Trim()},";
}
