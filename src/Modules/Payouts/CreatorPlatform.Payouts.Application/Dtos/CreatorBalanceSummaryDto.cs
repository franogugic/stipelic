using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Payouts.Application.Dtos;

public sealed record CreatorBalanceSummaryDto(
    Guid CreatorPublicId,
    string Name,
    string Slug,
    Currency Currency,
    int BalanceCents,
    bool HasPayoutProfile);
