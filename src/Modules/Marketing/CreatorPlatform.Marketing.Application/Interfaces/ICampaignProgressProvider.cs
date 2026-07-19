namespace CreatorPlatform.Marketing.Application.Interfaces;

public sealed record CampaignProgressDto(int SentCount, int FailedCount);

/// <summary>Reads send progress from the email outbox via the <c>{campaignPublicId}:{recipientId}</c>
/// CorrelationKey convention. <see cref="GetProgressAsync"/> takes the whole batch of campaign ids the
/// caller needs (a list page, or a single-element set for a detail view) and resolves all of them with
/// one aggregate SQL query — never one query per campaign.</summary>
public interface ICampaignProgressProvider
{
    Task<Dictionary<Guid, CampaignProgressDto>> GetProgressAsync(IReadOnlyCollection<Guid> campaignPublicIds, CancellationToken ct);
}
