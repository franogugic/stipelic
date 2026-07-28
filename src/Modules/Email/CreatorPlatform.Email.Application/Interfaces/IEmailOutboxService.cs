namespace CreatorPlatform.Email.Application.Interfaces;

public interface IEmailOutboxService
{
    Task QueueEmailVerificationAsync(string toEmail, string userPublicId, string token, CancellationToken ct);

    Task CancelUnsentEmailVerificationMessagesAsync(string userPublicId, CancellationToken ct);

    Task QueuePasswordResetAsync(string toEmail, string userPublicId, string token, CancellationToken ct);

    Task QueueOrderAccessAsync(string toEmail, string orderPublicId, string productName, string accessUrl, CancellationToken ct);

    Task QueuePayoutRequestedAsync(
        string toEmail,
        string payoutPublicId,
        string creatorName,
        string creatorSlug,
        int amountCents,
        string currency,
        CancellationToken ct);

    /// <summary>Queues one pre-rendered campaign broadcast message for a single recipient. Subject/html/
    /// plainText are expected to already have <c>{{UNSUBSCRIBE_URL}}</c> substituted with
    /// <paramref name="listUnsubscribeUrl"/> for this exact recipient — the caller renders the branded
    /// template once per campaign, not once per call to this method. Add-only, like every other Queue*
    /// method here — the caller's unit of work flushes it (send pipeline, Marketing module).</summary>
    Task QueueCampaignAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        string? replyTo,
        string listUnsubscribeUrl,
        string correlationKey,
        CancellationToken ct);
}
