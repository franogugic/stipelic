namespace CreatorPlatform.Email.Application.Interfaces;

public interface IEmailSender
{
    /// <summary><paramref name="replyTo"/> and <paramref name="listUnsubscribeUrl"/> are null for
    /// transactional mail. When <paramref name="listUnsubscribeUrl"/> is set, implementations must attach
    /// <c>List-Unsubscribe</c> + <c>List-Unsubscribe-Post</c> headers (RFC 8058 one-click).</summary>
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        string? replyTo,
        string? listUnsubscribeUrl,
        CancellationToken ct);
}
