namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>The "resend to people who didn't open" state of a campaign. Computed on the server; the client never
/// recomputes it. <see cref="Reason"/> is one of <c>NonOpenerResendReasons</c>, or null when
/// <see cref="Eligible"/>. <see cref="RecipientCount"/> is the live count for the next resend.</summary>
/// <param name="OpenedAnyCount">People who opened the original or any sent resend (each person once).</param>
/// <param name="OpenedThroughResendsCount">Of those, the people who opened a resend but not the original.</param>
public sealed record NonOpenerResendDto(
    bool Eligible,
    string? Reason,
    int RecipientCount,
    DateTimeOffset? EligibleFrom,
    DateTimeOffset? EligibleUntil,
    int Used,
    int Limit,
    int OpenedAnyCount,
    int OpenedThroughResendsCount,
    List<NonOpenerResendRowDto> Resends);

/// <summary>One resend of the original, oldest first (cancelled and failed ones included, with their status).</summary>
public sealed record NonOpenerResendRowDto(
    Guid PublicId,
    int Round,
    string Status,
    string Subject,
    DateTimeOffset? QueuedAt,
    DateTimeOffset? ScheduledAt,
    int RecipientCount,
    int DeliveredCount,
    int UniqueOpenCount);

/// <summary>The original a resend was made from.</summary>
public sealed record CampaignSourceDto(Guid PublicId, string Subject);
