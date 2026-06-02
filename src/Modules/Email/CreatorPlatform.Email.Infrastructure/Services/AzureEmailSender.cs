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

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken ct)
    {
        var emailMessage = new EmailMessage(
            senderAddress: _options.FromAddress,
            content: new EmailContent(subject)
            {
                PlainText = plainTextBody,
                Html = htmlBody
            },
            recipients: new EmailRecipients(new List<EmailAddress>
            {
                new(toEmail)
            }));

        await _emailClient.SendAsync(WaitUntil.Completed, emailMessage, ct);
    }
}
