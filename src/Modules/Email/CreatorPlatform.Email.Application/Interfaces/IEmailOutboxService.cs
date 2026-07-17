namespace CreatorPlatform.Email.Application.Interfaces;

public interface IEmailOutboxService
{
    Task QueueEmailVerificationAsync(string toEmail, string userPublicId, string token, CancellationToken ct);

    Task CancelUnsentEmailVerificationMessagesAsync(string userPublicId, CancellationToken ct);

    Task QueueOrderAccessAsync(string toEmail, string orderPublicId, string productName, string accessUrl, CancellationToken ct);

    Task QueuePayoutRequestedAsync(
        string toEmail,
        string payoutPublicId,
        string creatorName,
        string creatorSlug,
        int amountCents,
        string currency,
        CancellationToken ct);
}
