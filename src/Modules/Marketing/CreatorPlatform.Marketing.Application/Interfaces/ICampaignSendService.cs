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
    /// Pending, so the outbox worker retries them. Deliberately does NOT call
    /// <c>ICreatorUsageService.TryConsumeAsync</c> — the monthly send limit was already consumed once at
    /// the original send and refunded only on each message's permanent failure (see
    /// <c>CampaignBroadcastFailureHandler</c>); a manual resend is "try the same send again", not a new
    /// campaign, so it must not charge the limit a second time. This is an intentional asymmetry with the
    /// refund-on-fail behavior, not a bug — do not "fix" it by adding a TryConsumeAsync call here.
    /// No-ops (0 requeued) if the campaign currently has no Failed recipients.</summary>
    Task<ResendFailedResultDto> ResendFailedAsync(string slug, int ownerUserId, Guid campaignPublicId, CancellationToken ct);
}
