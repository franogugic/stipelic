using CreatorPlatform.Payouts.Application.Dtos;

namespace CreatorPlatform.Payouts.Application.Interfaces;

public sealed record CreatorPayoutSummaryDto(string Currency, int BalanceCents, int PendingPayoutCents, int MinPayoutCents);

public interface ICreatorPayoutService
{
    /// <summary>Null when the creator is StripeConnect-mode (no platform-held balance to show) — the
    /// controller returns 200 with null data so the frontend simply hides the balance card.</summary>
    Task<CreatorPayoutSummaryDto?> GetSummaryAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<List<PayoutDto>> GetHistoryAsync(string slug, int ownerUserId, CancellationToken ct);
}
