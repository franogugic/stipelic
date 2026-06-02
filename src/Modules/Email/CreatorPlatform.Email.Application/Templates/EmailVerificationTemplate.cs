namespace CreatorPlatform.Email.Application.Templates;

public static class EmailVerificationTemplate
{
    public const string Subject = "Verify your email address";

    public static string BuildHtml(string verificationUrl)
    {
        return $"""
            <h1>Verify your email address</h1>
            <p>Click the link below to verify your account:</p>
            <p><a href="{verificationUrl}">Verify email</a></p>
            <p>This link expires in 24 hours.</p>
            """;
    }

    public static string BuildPlainText(string verificationUrl)
    {
        return $"""
            Verify your email address

            Open this link to verify your account:
            {verificationUrl}

            This link expires in 24 hours.
            """;
    }
}
