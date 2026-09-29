namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>Open rate per calendar month (UTC, oldest first, one point per month of the window even when
/// nothing was sent). Rates are ratios (0.42 = 42%), null when nothing was delivered.</summary>
/// <param name="CurrentRate">The current month's rate.</param>
/// <param name="AverageRate">Weighted over the whole window — total opens / total delivered, not the mean
/// of the monthly rates.</param>
public sealed record OpenRateTrendDto(
    List<OpenRateTrendPointDto> Points,
    double? CurrentRate,
    double? AverageRate);

/// <param name="Month">"yyyy-MM".</param>
/// <param name="Sent">Emails actually delivered (outbox status Sent) for campaigns queued that month.</param>
/// <param name="Opens">Unique opens across those campaigns.</param>
public sealed record OpenRateTrendPointDto(string Month, double? Rate, int Sent, int Opens);
