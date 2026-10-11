namespace CreatorPlatform.Marketing.Application.Dtos;

/// <summary>Subscriber overview for one creator — headline counts, cumulative growth and a per-landing-page
/// breakdown. All-time and unfiltered.</summary>
/// <param name="Total">Every contact in the creator's directory.</param>
/// <param name="Active">Contacts that have not unsubscribed from this creator.</param>
/// <param name="NewThisMonth">Contacts first captured since the start of the current month in the creator's timezone.</param>
/// <param name="Unsubscribed">Opt-outs recorded for this creator (kept even when the contact itself was
/// deleted, so this can exceed <c>Total - Active</c>).</param>
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
