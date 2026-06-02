namespace CreatorPlatform.Email.Application.Interfaces;

public interface IEmailOutboxService
{
    Task QueueEmailVerificationAsync(string toEmail, string userPublicId, string token, CancellationToken ct);

    Task CancelUnsentEmailVerificationMessagesAsync(string userPublicId, CancellationToken ct);
}
