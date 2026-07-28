namespace CreatorPlatform.Email.Application.Templates;

public static class PasswordResetTemplate
{
    public const string Subject = "Reset your password";

    public static string BuildHtml(string resetUrl)
    {
        return $"""
            <h1>Reset your password</h1>
            <p>Click the link below to choose a new password:</p>
            <p><a href="{resetUrl}">Reset password</a></p>
            <p>This link expires in 1 hour. If you didn't request this, you can safely ignore this email.</p>
            """;
    }

    public static string BuildPlainText(string resetUrl)
    {
        return $"""
            Reset your password

            Open this link to choose a new password:
            {resetUrl}

            This link expires in 1 hour. If you didn't request this, you can safely ignore this email.
            """;
    }
}
