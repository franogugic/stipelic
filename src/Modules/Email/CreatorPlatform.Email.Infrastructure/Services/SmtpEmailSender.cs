using System.Net.Sockets;
using CreatorPlatform.Email.Application.Exceptions;
using CreatorPlatform.Email.Application.Interfaces;
using CreatorPlatform.Email.Infrastructure.Options;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CreatorPlatform.Email.Infrastructure.Services;

/// <summary>
/// Sends through an SMTP server — in Development the local Mailpit catcher. Builds the same message as
/// <see cref="AzureEmailSender"/>: From, To, Reply-To, Subject, an HTML and a plain-text part, and the RFC 8058
/// one-click unsubscribe headers when the outbox row has a URL. One connection per message: the outbox worker sends
/// a small batch every few seconds.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    /// <summary>How soon to try again when the server is unreachable or refuses temporarily (e.g. Mailpit not started).</summary>
    public static readonly TimeSpan RetryAfterUnavailable = TimeSpan.FromMinutes(1);

    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        string? replyTo,
        string? listUnsubscribeUrl,
        CancellationToken ct)
    {
        var message = BuildMessage(_options.FromAddress, toEmail, subject, htmlBody, plainTextBody, replyTo, listUnsubscribeUrl);
        var smtp = _options.Smtp;

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(smtp.Host, smtp.Port, smtp.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
            if (!string.IsNullOrEmpty(smtp.UserName))
                await client.AuthenticateAsync(smtp.UserName, smtp.Password!, ct);   // validated: set together
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(quit: true, ct);
        }
        catch (Exception exception) when (IsTransient(exception))
        {
            throw new TransientEmailSendException(
                $"The SMTP server {smtp.Host}:{smtp.Port} is not available right now: {exception.Message}",
                RetryAfterUnavailable,
                exception);
        }
    }

    public static MimeMessage BuildMessage(
        string fromAddress,
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        string? replyTo,
        string? listUnsubscribeUrl)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        if (!string.IsNullOrWhiteSpace(replyTo))
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        message.Subject = subject;

        if (!string.IsNullOrWhiteSpace(listUnsubscribeUrl))
        {
            message.Headers.Add("List-Unsubscribe", $"<{listUnsubscribeUrl}>");
            message.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        }

        // multipart/alternative: plain text first, HTML last (the preferred part).
        message.Body = new BodyBuilder { TextBody = plainTextBody, HtmlBody = htmlBody }.ToMessageBody();
        return message;
    }

    /// <summary>Can't connect, the connection dropped, or a 4xx reply (RFC 5321: temporary). A 5xx reply — e.g. a refused
    /// recipient — is a real failure and goes through the outbox's normal retries.</summary>
    private static bool IsTransient(Exception exception) => exception switch
    {
        OperationCanceledException => false,
        SocketException or IOException or ServiceNotConnectedException or SslHandshakeException => true,
        SmtpCommandException command => (int)command.StatusCode is >= 400 and < 500,
        SmtpProtocolException => true,
        _ => false
    };
}
