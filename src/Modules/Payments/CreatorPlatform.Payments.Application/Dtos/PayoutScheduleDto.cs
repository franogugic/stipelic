namespace CreatorPlatform.Payments.Application.Dtos;

/// <summary>A Connect account's automatic payout schedule, as Stripe reports it.</summary>
/// <param name="Interval">"manual", "daily", "weekly" or "monthly".</param>
/// <param name="DelayDays">Days a charge waits before it is paid out.</param>
/// <param name="WeeklyAnchor">The weekday of a weekly schedule (e.g. "monday"); null otherwise.</param>
/// <param name="MonthlyAnchor">The day of the month of a monthly schedule (1–31); null otherwise.</param>
public sealed record PayoutScheduleDto(string Interval, int DelayDays, string? WeeklyAnchor, int? MonthlyAnchor);
