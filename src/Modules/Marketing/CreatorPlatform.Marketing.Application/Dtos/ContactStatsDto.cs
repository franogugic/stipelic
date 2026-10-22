namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>Subscriber overview for one creator — headline counts, cumulative growth and a per-landing-page
/// breakdown. All-time and unfiltered.</summary>
/// <param name="Total">Every contact in the creator's directory.</param>
/// <param name="Active">Contacts that have not unsubscribed from this creator.</param>
/// <param name="NewThisMonth">Contacts first captured since the start of the current month in the creator's timezone.</param>
/// <param name="Unsubscribed">Contacts that have unsubscribed from this creator, so
/// <c>Total = Active + Unsubscribed</c>. An opt-out of a deleted contact, or of an address that never subscribed,
/// is not counted here (it still appears in <c>Monthly</c>, which describes opt-out events).</param>
/// <param name="Growth">One point per calendar month (creator timezone) of the last 12, oldest first, current month
/// last.</param>
/// <param name="Sources">Every landing page (archived included) with at least one contact, most contacts
/// first.</param>
public sealed record ContactStatsDto(
    int Total,
    int Active,
    int NewThisMonth,
    int Unsubscribed,
    List<ContactGrowthPointDto> Growth,
    List<ContactSourceCountDto> Sources,
    List<ContactMonthDto> Monthly);

/// <param name="Month">"yyyy-MM" in the creator's timezone; the last 12 months, oldest first, current month last.</param>
/// <param name="Joined">Contacts first captured in the month (deleted contacts are no longer counted).</param>
/// <param name="Unsubscribed">Opt-outs recorded in the month (kept even when the contact was deleted).</param>
public sealed record ContactMonthDto(string Month, int Joined, int Unsubscribed);

/// <param name="Month">"yyyy-MM".</param>
/// <param name="Total">Cumulative contacts first captured before the end of that month — includes everyone
/// captured before the window started.</param>
public sealed record ContactGrowthPointDto(string Month, int Total);

/// <param name="Count">Contacts captured on that landing page — a contact captured on several pages is
/// counted once on each.</param>
public sealed record ContactSourceCountDto(Guid LandingPagePublicId, string Title, int Count);

/// <summary>The subscriber overview with the growth chart cut to one period — the answer of
/// <c>GET contacts/stats?period=</c>. The headline counts and <paramref name="Sources"/> are the same all-time figures
/// as in <see cref="ContactStatsDto"/>; <paramref name="Buckets"/> replaces its <c>Growth</c> and <c>Monthly</c>.</summary>
/// <param name="Period">"today" | "7d" | "15d" | "30d" | "2m" | "3m" | "6m" | "all".</param>
/// <param name="Step">"hour" | "day" | "week" | "month" — the bucket size.</param>
/// <param name="Timezone">The IANA zone the buckets are in.</param>
/// <param name="Range">The period: the day and long periods end now; "all" starts when the workspace was created.</param>
/// <param name="Buckets">Oldest first, zero-filled: every hour so far for "today", every day, 7-day weeks anchored at the
/// window's first day for 2m / 3m / 6m, or the last 12 months for "all".</param>
public sealed record ContactPeriodStatsDto(
    int Total,
    int Active,
    int NewThisMonth,
    int Unsubscribed,
    List<ContactSourceCountDto> Sources,
    string Period,
    string Step,
    string Timezone,
    ContactPeriodRangeDto Range,
    List<ContactBucketDto> Buckets);

public sealed record ContactPeriodRangeDto(DateTimeOffset From, DateTimeOffset To);

/// <param name="Start">The bucket's local start, "yyyy-MM-ddTHH:mm" in the stats' timezone.</param>
/// <param name="Joined">Contacts first captured in the bucket (deleted contacts are no longer counted).</param>
/// <param name="Unsubscribed">Opt-outs recorded in the bucket (kept even when the contact was deleted).</param>
/// <param name="Total">Contacts first captured before the bucket ended — includes everyone captured before the period
/// started, so the series is the running size of the directory.</param>
public sealed record ContactBucketDto(string Start, int Joined, int Unsubscribed, int Total);
