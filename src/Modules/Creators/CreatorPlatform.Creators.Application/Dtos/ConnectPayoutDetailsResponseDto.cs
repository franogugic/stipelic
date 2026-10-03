using CreatorPlatform.Payments.Application.Dtos;

namespace CreatorPlatform.Creators.Application.Dtos;

/// <summary>The workspace's Stripe Connect payout details. <paramref name="AccountId"/> is null until onboarding
/// has created the account; <paramref name="PayoutSchedule"/> is null without an account or when Stripe can't be
/// reached.</summary>
public sealed record ConnectPayoutDetailsResponseDto(
    string? AccountId,
    DateTimeOffset? DetailsSubmittedAt,
    DateTimeOffset? PayoutsEnabledAt,
    PayoutScheduleDto? PayoutSchedule);
