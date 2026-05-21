namespace CreatorPlatform.Email.Application.Interfaces;

public interface IEmailOutboxService
{
    Task QueueEmailVerificationAsync(string toEmail, string token, CancellationToken ct);
}
