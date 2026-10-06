namespace CreatorPlatform.Payouts.Application.Dtos;

/// <param name="WorkspaceStatus">The creator's status ("Active", "PendingPayment", "Suspended", "Disabled"). A deleted
/// (Disabled) workspace is still listed while money is owed to it, so the admin can pay it out.</param>
public sealed record CreatorBalanceSummaryDto(
    Guid CreatorPublicId,
    string Name,
    string Slug,
    string Currency,
    int BalanceCents,
    bool HasPayoutProfile,
    string WorkspaceStatus);
