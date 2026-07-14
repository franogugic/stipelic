using CreatorPlatform.Shared.Domain.Enums;

namespace CreatorPlatform.Payouts.Application.Dtos;

public sealed record CreatorBalanceDto(Currency Currency, int BalanceCents);
