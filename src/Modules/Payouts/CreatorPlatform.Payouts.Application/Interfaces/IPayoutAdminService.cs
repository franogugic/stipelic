using CreatorPlatform.Payouts.Application.Dtos;

namespace CreatorPlatform.Payouts.Application.Interfaces;

public interface IPayoutAdminService
{
    /// <summary>BankTransfer creators whose balance is at least <paramref name="minCents"/> (defaults to
    /// <c>PayoutsOptions.MinPayoutCents</c> when null), sorted by balance descending.</summary>
    Task<List<CreatorBalanceSummaryDto>> GetBalancesAsync(int? minCents, int limit, CancellationToken ct);

    /// <summary>Admin queue: payouts with full (unmasked) IBAN so the platform admin can execute the bank
    /// transfer. <paramref name="status"/> is one of Pending/Paid/Failed/Cancelled (case-insensitive) or
    /// null for all statuses.</summary>
    Task<List<AdminPayoutQueueItemDto>> ListQueueAsync(string? status, int limit, CancellationToken ct);

    Task<PayoutDto> CreatePayoutAsync(CreatePayoutRequestDto request, CancellationToken ct);

    Task<PayoutDto> MarkPaidAsync(Guid payoutPublicId, MarkPayoutPaidRequestDto request, CancellationToken ct);

    Task<PayoutDto> MarkFailedAsync(Guid payoutPublicId, MarkPayoutFailedRequestDto request, CancellationToken ct);
}
