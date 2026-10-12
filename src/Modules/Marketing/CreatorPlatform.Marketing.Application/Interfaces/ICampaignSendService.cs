using CreatorPlatform.Marketing.Application.Dtos;

namespace CreatorPlatform.Marketing.Application.Interfaces;

/// <summary>Sends an Active template to an audience — either immediately (snapshots content into a new
/// Queued <see cref="Campaigns.Campaign"/> row and fans out into the email outbox in one shot) or on a
/// schedule (snapshots content into a Scheduled row now; the audience/limit/outbox pipeline runs only at
/// dispatch time, via <see cref="DispatchScheduledAsync"/>, so a late unsubscribe is always honored). See
/// implementation for the exact transactional flow.</summary>
public interface ICampaignSendService
{
    Task<CampaignDetailDto> SendAsync(string slug, int ownerUserId, SendCampaignRequestDto request, CancellationToken ct);

    /// <summary>Called by the background <c>ScheduledCampaignDispatchWorker</c>, never by an HTTP
    /// controller — no ownership check (the caller already trusts <paramref name="campaignPublicId"/> off
    /// a due DB row). Tolerant of a campaign that's no longer Scheduled (already dispatched or cancelled
    /// by a racing process) — logs and returns rather than throwing, since the poller must keep going.
    /// On pipeline failure, marks the campaign Failed with a truncated reason instead of rethrowing.</summary>
    Task DispatchScheduledAsync(Guid campaignPublicId, CancellationToken ct);

    /// <summary>Creator-initiated cancellation of their own still-Scheduled send. Ownership-checked like
    /// every other endpoint; throws <see cref="Shared.Application.Exceptions.ConflictException"/> if the
    /// campaign is no longer Scheduled (already dispatched/failed/cancelled) — unlike the tolerant
    /// no-op in <see cref="DispatchScheduledAsync"/>, this IS an HTTP path where a stale attempt should
    /// surface as an error to the caller.</summary>
    Task<CampaignDetailDto> CancelScheduledAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);

    /// <summary>Manually requeues every currently-Failed recipient of one already-sent campaign back to
    /// Pending, so the outbox worker retries them. Each permanently failed message was refunded to the monthly
    /// sends (see <c>CampaignBroadcastFailureHandler</c>), so sending it again consumes one send again — in the
    /// campaign's own period, the one a repeated failure is refunded to — and the counter always equals what
    /// was delivered. Runs under the creator's campaign lock, so concurrent resends requeue and consume each
    /// message once. Over the limit → 409 <c>email_limit_reached</c> with the remaining count, nothing requeued.
    /// No-ops (0 requeued) if the campaign currently has no Failed recipients.</summary>
    Task<ResendFailedResultDto> ResendFailedAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);
}
