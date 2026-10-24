using CreatorPlatform.Email.Application.Templates;

namespace CreatorPlatform.Email.Application.Interfaces;

public interface IEmailOutboxService
{
    Task QueueEmailVerificationAsync(string toEmail, string? firstName, string userPublicId, string token, CancellationToken ct);

    Task CancelUnsentEmailVerificationMessagesAsync(string userPublicId, CancellationToken ct);

    Task QueuePasswordResetAsync(string toEmail, string? firstName, string userPublicId, string token, CancellationToken ct);

    /// <summary>The confirmation link for a requested email change, sent to the NEW address.</summary>
    Task QueueEmailChangeVerificationAsync(
        string toEmail, string? firstName, string currentEmail, string userPublicId, string token, CancellationToken ct);

    /// <summary>"Your email was changed" notice, sent to the OLD address after a confirmed change.</summary>
    Task QueueEmailChangedNotificationAsync(
        string toEmail, string? firstName, string userPublicId, string newEmail, DateTimeOffset changedAt, CancellationToken ct);

    /// <summary>The buyer's creator-branded order email; replies go to the creator's support address when set.</summary>
    Task QueueOrderAccessAsync(string toEmail, string orderPublicId, OrderAccessEmail order, CancellationToken ct);

    /// <summary>The "your free download" email for a lead-page sign-up (transactional: sent even to an unsubscribed
    /// email); replies go to the creator's support address when set.</summary>
    Task QueueLeadMagnetAsync(string toEmail, string correlationKey, LeadMagnetEmail lead, CancellationToken ct);

    /// <summary>Queues one pre-rendered campaign broadcast message for a single recipient. Subject/html/
    /// plainText are expected to already have <c>{{UNSUBSCRIBE_URL}}</c> substituted with
    /// <paramref name="listUnsubscribeUrl"/> for this exact recipient — the caller renders the branded
    /// template once per campaign, not once per call to this method. Add-only, like every other Queue*
    /// method here — the caller's unit of work flushes it (send pipeline, Marketing module).
    ///
    /// <paramref name="creatorId"/> is the unit of sending fairness: each message gets the next position in that creator's
    /// share of the campaign queue. Call it inside the creator's campaign lock, so two campaigns of one creator are
    /// numbered one after the other.</summary>
    Task QueueCampaignAsync(
        int creatorId,
        string toEmail,
        string subject,
        string htmlBody,
        string plainTextBody,
        string? replyTo,
        string listUnsubscribeUrl,
        string correlationKey,
        CancellationToken ct);
}
