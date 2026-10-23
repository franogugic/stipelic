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
/// <param name="ImportedThisMonth">Contacts a CSV import created since the start of the current month (creator timezone);
/// they are part of <paramref name="NewThisMonth"/> too.</param>
/// <param name="Imported">Contacts a CSV import created, all time — the "Imported" source next to
/// <paramref name="Sources"/>.</param>
public sealed record ContactStatsDto(
    int Total,
    int Active,
    int NewThisMonth,
    int Unsubscribed,
    List<ContactGrowthPointDto> Growth,
    List<ContactSourceCountDto> Sources,
    List<ContactMonthDto> Monthly,
    int ImportedThisMonth,
    int Imported);

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

/// <summary>The subscriber overview of one period — the answer of <c>GET contacts/stats?period=</c>. The headline counts
/// (<paramref name="Total"/>, <paramref name="Active"/>, …) stay all-time; the rest is the period's (R6.6).</summary>
/// <param name="ImportedThisMonth">As in <see cref="ContactStatsDto"/> — all-time figures, not cut to the period.</param>
/// <param name="Imported">As in <see cref="ContactStatsDto"/> — all-time figures, not cut to the period.</param>
/// <param name="Sources">Who joined in the period, each contact once, at the source of its first capture (a page, or an
/// import); the rows add up to <paramref name="Joined"/>.</param>
/// <param name="Period">"today" | "7d" | "15d" | "30d" | "2m" | "3m" | "6m" | "all".</param>
/// <param name="Step">"hour" | "day" | "week" | "month" — the bucket size.</param>
/// <param name="Timezone">The IANA zone the buckets are in.</param>
/// <param name="Range">The period: the day and long periods end now; "all" starts when the workspace was created.</param>
/// <param name="Buckets">Oldest first, zero-filled: every hour so far for "today", every day, 7-day weeks for 2m / 3m and
/// months for 6m (anchored at the window's first day, the day after the same date N months ago), or the last 12 months
/// for "all".</param>
/// <param name="PreviousRange">The window before; null for "all".</param>
/// <param name="StartTotal">Contacts in the directory when the period started (first captured before
/// <paramref name="Range"/>.From; deleted contacts not counted); 0 for "all".</param>
/// <param name="Joined">New contacts in the period.</param>
/// <param name="UnsubscribedInPeriod">Opt-outs recorded in the period (also of contacts deleted since).</param>
/// <param name="ImportedInPeriod">Of <paramref name="Joined"/>, contacts a CSV import created.</param>
/// <param name="Previous">The same three for <paramref name="PreviousRange"/>; null for "all".</param>
public sealed record ContactPeriodStatsDto(
    int Total,
    int Active,
    int NewThisMonth,
    int Unsubscribed,
    List<ContactPeriodSourceDto> Sources,
    string Period,
    string Step,
    string Timezone,
    ContactPeriodRangeDto Range,
    List<ContactBucketDto> Buckets,
    int ImportedThisMonth,
    int Imported,
    ContactPeriodRangeDto? PreviousRange,
    int StartTotal,
    int Joined,
    int UnsubscribedInPeriod,
    int ImportedInPeriod,
    ContactPeriodPreviousDto? Previous);

public sealed record ContactPeriodRangeDto(DateTimeOffset From, DateTimeOffset To);

public sealed record ContactPeriodPreviousDto(int Joined, int Unsubscribed, int Imported);

/// <param name="Kind">"page" | "import".</param>
/// <param name="LandingPagePublicId">The page; null for the import row.</param>
/// <param name="Title">The page title; "Import" for the import row.</param>
public sealed record ContactPeriodSourceDto(string Kind, Guid? LandingPagePublicId, string Title, int Count);

/// <param name="Start">The bucket's local start, "yyyy-MM-ddTHH:mm" in the stats' timezone.</param>
/// <param name="Joined">Contacts first captured in the bucket (deleted contacts are no longer counted).</param>
/// <param name="Unsubscribed">Opt-outs recorded in the bucket (kept even when the contact was deleted).</param>
/// <param name="Total">Contacts first captured before the bucket ended — includes everyone captured before the period
/// started, so the series is the running size of the directory.</param>
/// <param name="Imported">Of <paramref name="Joined"/>, contacts a CSV import created (R6.6).</param>
public sealed record ContactBucketDto(string Start, int Joined, int Unsubscribed, int Total, int Imported = 0);
