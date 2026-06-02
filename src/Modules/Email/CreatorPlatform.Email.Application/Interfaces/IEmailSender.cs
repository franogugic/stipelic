namespace CreatorPlatform.Email.Application.Interfaces;

public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        CancellationToken ct);
}
