using Azure;
using Azure.Communication.Email;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CreatorPlatform.Email.Infrastructure.Services;

public sealed class AzureEmailSender : IEmailSender
{
    private readonly EmailClient _emailClient;
    private readonly EmailOptions _options;

    public AzureEmailSender(EmailClient emailClient, IOptions<EmailOptions> options)
    {
        _emailClient = emailClient;
        _options = options.Value;
    }

    public async Task SendEmailVerificationAsync(string toEmail, string token, CancellationToken ct)
    {
        var verificationUrl = BuildVerificationUrl(token);

        var subject = "Verify your email address";
        var htmlContent = $"""
            <h1>Verify your email address</h1>
            <p>Click the link below to verify your account:</p>
            <p><a href="{verificationUrl}">Verify email</a></p>
            <p>This link expires in 24 hours.</p>
            """;

        var plainTextContent = $"""
            Verify your email address

            Open this link to verify your account:
            {verificationUrl}

            This link expires in 24 hours.
            """;

        var emailMessage = new EmailMessage(
            senderAddress: _options.FromAddress,
            content: new EmailContent(subject)
            {
                PlainText = plainTextContent,
                Html = htmlContent
            },
            recipients: new EmailRecipients(new List<EmailAddress>
            {
                new(toEmail)
            }));

        await _emailClient.SendAsync(WaitUntil.Completed, emailMessage, ct);
    }

    // raw token??
    private string BuildVerificationUrl(string token)
    {
        var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');
        var encodedToken = Uri.EscapeDataString(token);

        return $"{baseUrl}/verify-email?token={encodedToken}";
    }
}
