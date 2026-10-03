using CreatorPlatform.Payouts.Application.Dtos;

namespace CreatorPlatform.Payouts.Application.Interfaces;

/// <param name="TotalPaidOutCents">Sum of Paid payouts in <paramref name="Currency"/> (the balance's currency).</param>
/// <param name="PendingRequest">The open payout request (at most one), or null.</param>
public sealed record CreatorPayoutSummaryDto(
    string Currency,
    int BalanceCents,
    int PendingPayoutCents,
    int MinPayoutCents,
    int TotalPaidOutCents,
    PendingPayoutRequestDto? PendingRequest);

/// <summary>The creator's open payout request; <paramref name="PublicId"/> is what the cancel endpoint takes.</summary>
public sealed record PendingPayoutRequestDto(Guid PublicId, int AmountCents, DateTimeOffset RequestedAt);

public interface ICreatorPayoutService
{
    /// <summary>Null when the creator is StripeConnect-mode (no platform-held balance to show) — the
    /// controller returns 200 with null data so the frontend simply hides the balance card.</summary>
    Task<CreatorPayoutSummaryDto?> GetSummaryAsync(string slug, int ownerUserId, CancellationToken ct);

    Task<List<PayoutDto>> GetHistoryAsync(string slug, int ownerUserId, CancellationToken ct);

    /// <summary>Creator self-serve payout request. <paramref name="amountCents"/> null means "request the
    /// full available balance". Queues an admin notification email in the same transaction as the payout.</summary>
    Task<PayoutDto> RequestPayoutAsync(string slug, int ownerUserId, int? amountCents, CancellationToken ct);

    /// <summary>Cancels the creator's own still-Pending payout request, restoring the reserved balance
    /// via a compensating ledger Adjustment in the same transaction.</summary>
    Task<PayoutDto> CancelPayoutRequestAsync(string slug, int ownerUserId, Guid payoutPublicId, CancellationToken ct);
}
