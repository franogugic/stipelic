namespace CreatorPlatform.Email.Application.Templates;

public static class OrderAccessTemplate
{
    public const string Subject = "Your purchase is ready";

    public static string BuildHtml(string productName, string accessUrl)
    {
        return $"""
            <h1>Thank you for your purchase!</h1>
            <p>Your access to <strong>{productName}</strong> is ready.</p>
            <p><a href="{accessUrl}">Access your product</a></p>
            """;
    }

    public static string BuildPlainText(string productName, string accessUrl)
    {
        return $"""
            Thank you for your purchase!

            Your access to {productName} is ready.

            Open this link to access your product:
            {accessUrl}
            """;
    }
}
