namespace CreatorPlatform.Email.Application.Templates;

/// <summary>Sent to the NEW address: confirming proves the user controls it.</summary>
public static class EmailChangeVerificationTemplate
{
    public const string Subject = "Confirm your new email address";

    public static string BuildHtml(string confirmationUrl)
    {
        return $"""
            <h1>Confirm your new email address</h1>
            <p>Click the link below to use this address to sign in to Luma:</p>
            <p><a href="{confirmationUrl}">Confirm email change</a></p>
            <p>This link expires in 24 hours. If you didn't ask for this, ignore this email — nothing changes.</p>
            """;
    }

    public static string BuildPlainText(string confirmationUrl)
    {
        return $"""
            Confirm your new email address

            Open this link to use this address to sign in to Luma:
            {confirmationUrl}

            This link expires in 24 hours. If you didn't ask for this, ignore this email — nothing changes.
            """;
    }
}
