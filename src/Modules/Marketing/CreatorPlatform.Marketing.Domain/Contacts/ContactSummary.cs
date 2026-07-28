namespace CreatorPlatform.Marketing.Domain.Contacts;

/// <summary>Materialized per-(creator, email) summary of captures across all of the creator's landing
/// pages — maintained incrementally at capture time (see <c>EmailCaptureService</c>, Analytics module),
/// the same pattern as <c>CreatorUsageCounter</c>. Exists so the Contacts directory read
/// (<c>ContactsRepository.SearchAsync</c>) never has to re-aggregate the creator's full capture history on
/// every page load — it only ever reads/writes this table. Every mutation happens via raw SQL upsert (see
/// <c>IEmailCaptureRepository.UpsertContactSummaryAsync</c>), never through this type directly, so there
/// are deliberately no public mutators here — it exists purely as an EF-mapped read shape.</summary>
public sealed class ContactSummary
{
    private ContactSummary()
    {
    }

    public int Id { get; private set; }

    public int CreatorId { get; private set; }

    public string Email { get; private set; } = string.Empty;

    public DateTimeOffset FirstCapturedAt { get; private set; }

    public DateTimeOffset LastCapturedAt { get; private set; }

    /// <summary>Distinct internal landing page ids this email has been captured on, for this creator.
    /// Names are resolved by the caller (bounded batch join to landing_pages), never stored here — a
    /// landing page's title can change, and this array must never go stale.</summary>
    public List<int> SourceLandingPageIds { get; private set; } = [];
}
