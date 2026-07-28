namespace CreatorPlatform.Payouts.Application.Dtos;

public sealed record CreatorBalanceSummaryDto(
    Guid CreatorPublicId,
    string Name,
    string Slug,
    string Currency,
    int BalanceCents,
    bool HasPayoutProfile);
