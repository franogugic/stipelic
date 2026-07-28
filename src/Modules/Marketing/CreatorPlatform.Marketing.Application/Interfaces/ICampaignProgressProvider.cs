namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record CampaignProgressDto(int SentCount, int FailedCount);

public sealed record FailedRecipientDto(string Email, string? LastError);

/// <summary>Reads send progress from the email outbox via the <c>{campaignPublicId}:{recipientId}</c>
/// CorrelationKey convention. <see cref="GetProgressAsync"/> takes the whole batch of campaign ids the
/// caller needs (a list page, or a single-element set for a detail view) and resolves all of them with
/// one aggregate SQL query — never one query per campaign.</summary>
public interface ICampaignProgressProvider
{
    Task<Dictionary<Guid, CampaignProgressDto>> GetProgressAsync(IReadOnlyCollection<Guid> campaignPublicIds, CancellationToken ct);

    /// <summary>Every terminally-Failed recipient for one campaign, with its last error — one query, no
    /// pagination (bounded by the campaign's own recipient count).</summary>
    Task<List<FailedRecipientDto>> GetFailedRecipientsAsync(Guid campaignPublicId, CancellationToken ct);

    /// <summary>Requeues every currently-Failed outbox message for one campaign back to Pending (tracked
    /// query — caller must still call SaveChangesAsync). Returns how many were requeued. Deliberately does
    /// NOT touch the monthly send usage counter — see <see cref="ICampaignSendService.ResendFailedAsync"/>
    /// for why.</summary>
    Task<int> RequeueFailedAsync(Guid campaignPublicId, DateTimeOffset nextAttemptAt, CancellationToken ct);
}
