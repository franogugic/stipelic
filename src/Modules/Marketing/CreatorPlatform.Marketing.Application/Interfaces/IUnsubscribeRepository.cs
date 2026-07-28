using CreatorPlatform.Marketing.Domain.Unsubscribes;

namespace CreatorPlatform.Marketing.Application.Interfaces;

public interface IUnsubscribeRepository
{
    /// <summary>Idempotent insert — <c>ON CONFLICT (CreatorId, Email) DO NOTHING</c>, same pattern as the
    /// analytics tables. Safe to call repeatedly for the same creator/email pair (e.g. a recipient
    /// clicking an old unsubscribe link twice).</summary>
    Task AddIfNotExistsAsync(Unsubscribe unsubscribe, CancellationToken ct);

    Task<bool> ExistsAsync(int creatorId, string email, CancellationToken ct);
}
