namespace CreatorPlatform.Analytics.Domain.PageViews;

/// <summary>Incrementally-maintained all-time page-view count per creator, across all of their landing
/// pages — same pattern as <c>ContactSummary</c>/<c>CreatorUsageCounter</c>: a live COUNT over
/// <c>page_views</c> keeps getting more expensive as that table grows, so the total is kept as a running
/// counter instead. Mutated only via the atomic upsert in <c>IPageViewRepository.AddAsync</c> (incremented
/// exactly once per successfully-inserted page view, never on a deduped conflict) — no public mutators
/// here, this exists purely as an EF-mapped read shape.</summary>
public sealed class CreatorViewTotal
{
    private CreatorViewTotal()
    {
    }

    public int CreatorId { get; private set; }

    public int TotalViews { get; private set; }
}
