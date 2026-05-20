namespace CreatorPlatform.Email.Application.Interfaces;

public interface IEmailSender
{
    Task SendEmailVerificationAsync(string toEmail, string token, CancellationToken ct);
}
