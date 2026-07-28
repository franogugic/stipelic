using CreatorPlatform.Email.Domain.Outbox;

namespace CreatorPlatform.Email.Application.Interfaces;

/// <summary>Dispatched by the outbox worker exactly once, at the moment a message transitions to
/// terminal <see cref="EmailOutboxMessageStatus.Failed"/> — never for retries still in progress, and
/// never for provider-throttled (429) reschedules. One handler per <see cref="Purpose"/>; the worker
/// looks up the matching handler (if any) and calls it in the same database transaction as the status
/// transition, so a failed send and its side effect (e.g. refunding a consumed usage counter) commit or
/// roll back together. Implementations must not throw for "nothing to do" cases (unknown/unparseable
/// correlation, already-deleted parent record) — log and return so one bad message never blocks the
/// worker's batch.</summary>
public interface IEmailSendFailureHandler
{
    EmailOutboxMessagePurpose Purpose { get; }

    Task HandleAsync(EmailOutboxMessage message, CancellationToken ct);
}
